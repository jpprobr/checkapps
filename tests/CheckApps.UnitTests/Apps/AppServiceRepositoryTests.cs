using CheckApps.Application;
using CheckApps.Application.Apps;
using CheckApps.Domain;
using CheckApps.Domain.Discovery;
using CheckApps.UnitTests.TestDoubles;

namespace CheckApps.UnitTests.Apps;

public class AppServiceRepositoryTests
{
    private readonly FakeAppRepository _apps = new();
    private readonly AppService _service;

    public AppServiceRepositoryTests()
    {
        _apps.Add(new App { Id = "app-1", SquadId = "sq-1", Name = "App 1" });
        _apps.Add(new App { Id = "app-2", SquadId = "sq-1", Name = "App 2" });
        _service = new AppService(_apps, new FakeSquadRepository(), new FakeQualitySnapshotRepository(), new FakeCheckRepository(), new FakeJourneyRepository());
    }

    private static RepositoryInput Input(string? name = "repo", string? siglaApp = null) =>
        new(name, "github", "https://example.com/r", null, "ab1", siglaApp, "aplicacao");

    [Fact]
    public async Task Create_SetsManualSource_NewId_AndTrimsFields()
    {
        var repo = await _service.CreateRepositoryAsync("app-1", Input("  repo-a  ", " AB1 "));

        Assert.Equal("repo-a", repo.Name);
        Assert.Equal("AB1", repo.SiglaApp);
        Assert.Equal(RepositorySources.Manual, repo.Source);
        Assert.Equal("active", repo.Status);
        Assert.Null(repo.ExternalId);
        Assert.False(string.IsNullOrEmpty(repo.Id));
        Assert.Single(await _service.ListRepositoriesAsync("app-1"));
    }

    [Fact]
    public async Task Create_Throws_WhenAppMissing_OrNameBlank()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.CreateRepositoryAsync("nope", Input()));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateRepositoryAsync("app-1", Input("  ")));
    }

    [Fact]
    public async Task Create_Conflicts_WhenSiglaAppBelongsToAnotherRepository()
    {
        await _service.CreateRepositoryAsync("app-1", Input("a", "AB1"));

        await Assert.ThrowsAsync<DomainConflictException>(() => _service.CreateRepositoryAsync("app-2", Input("b", "AB1")));
    }

    [Fact]
    public async Task Update_KeepsSourceAndExternalId_AndAllowsOwnSiglaApp()
    {
        var scanned = new Repository
        {
            Id = "r1", AppId = "app-1", Name = "old", Provider = "github", ExternalId = "123",
            Source = RepositorySources.GithubScan, SiglaApp = "AB1",
        };
        await _apps.AddRepositoryAsync(scanned);

        var updated = await _service.UpdateRepositoryAsync("app-1", "r1", Input("new", "AB1"));

        Assert.Equal("new", updated.Name);
        Assert.Equal("123", updated.ExternalId);
        Assert.Equal(RepositorySources.GithubScan, updated.Source);
    }

    [Fact]
    public async Task Update_Conflicts_OnAnotherRepositorysSiglaApp_AndThrowsWhenMissing()
    {
        await _service.CreateRepositoryAsync("app-1", Input("a", "AB1"));
        var other = await _service.CreateRepositoryAsync("app-2", Input("b", "CD2"));

        await Assert.ThrowsAsync<DomainConflictException>(() => _service.UpdateRepositoryAsync("app-2", other.Id, Input("b", "AB1")));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.UpdateRepositoryAsync("app-2", "nope", Input()));
    }

    [Fact]
    public async Task Delete_RemovesRepository_AndThrowsWhenMissing()
    {
        var repo = await _service.CreateRepositoryAsync("app-1", Input());

        await _service.DeleteRepositoryAsync("app-1", repo.Id);

        Assert.Empty(await _service.ListRepositoriesAsync("app-1"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.DeleteRepositoryAsync("app-1", repo.Id));
    }

    private static RepositoryInput Active(string name, string? role, string? status = "active") =>
        new(name, "github", null, status, null, null, role);

    [Fact]
    public async Task Create_AsActive_ConflictsWithTheActiveOfTheSameRole_AndChangesNothing()
    {
        var infra = await _service.CreateRepositoryAsync("app-1", Active("infra-1", "infra"));
        var app1 = await _service.CreateRepositoryAsync("app-1", Active("app-1", "aplicacao"));

        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => _service.CreateRepositoryAsync("app-1", Active("app-2", "aplicacao")));

        Assert.Contains("'app-1'", ex.Message);
        Assert.Contains("aplicacao", ex.Message);
        var all = await _service.ListRepositoriesAsync("app-1");
        Assert.Equal(2, all.Count);   // o novo não foi gravado
        Assert.Equal("active", all.Single(r => r.Id == app1.Id).Status);   // ninguém foi rebaixado
        Assert.Equal("active", all.Single(r => r.Id == infra.Id).Status);
    }

    [Fact]
    public async Task Create_AsActive_IsAllowedForAnotherRole_AnotherApp_OrWhenNotActive()
    {
        await _service.CreateRepositoryAsync("app-1", Active("a", "aplicacao"));

        await _service.CreateRepositoryAsync("app-1", Active("b", "infra"));
        await _service.CreateRepositoryAsync("app-2", Active("c", "aplicacao"));
        await _service.CreateRepositoryAsync("app-1", Active("d", "aplicacao", status: "legacy"));
        await _service.CreateRepositoryAsync("app-1", Active("e", "aplicacao", status: "archived"));

        Assert.Equal(4, (await _service.ListRepositoriesAsync("app-1")).Count);
    }

    [Fact]
    public async Task NoRole_IsOneGroupOnly_AndRoleComparisonIgnoresCase()
    {
        await _service.CreateRepositoryAsync("app-1", Active("a", null));
        await _service.CreateRepositoryAsync("app-1", Active("c", "Infra"));

        var noRole = await Assert.ThrowsAsync<DomainConflictException>(
            () => _service.CreateRepositoryAsync("app-1", Active("b", null)));
        var infra = await Assert.ThrowsAsync<DomainConflictException>(
            () => _service.CreateRepositoryAsync("app-1", Active("d", "infra")));

        Assert.Contains("sem papel", noRole.Message);
        Assert.Contains("'c'", infra.Message);
        Assert.All(await _service.ListRepositoriesAsync("app-1"), r => Assert.Equal("active", r.Status));
    }

    [Fact]
    public async Task Activate_ConflictsWhileAnotherIsActive_AndWorksAfterTheOtherBecomesLegacy()
    {
        var oldRepo = await _service.CreateRepositoryAsync("app-1", Active("velho", "aplicacao"));
        var newRepo = await _service.CreateRepositoryAsync("app-1", Active("novo", "aplicacao", status: "legacy"));

        var ex = await Assert.ThrowsAsync<DomainConflictException>(() => _service.ActivateRepositoryAsync("app-1", newRepo.Id));
        Assert.Contains("'velho'", ex.Message);
        Assert.Equal("active", (await _service.ListRepositoriesAsync("app-1")).Single(r => r.Id == oldRepo.Id).Status);

        await _service.UpdateRepositoryAsync("app-1", oldRepo.Id, Active("velho", "aplicacao", status: "legacy"));
        var activated = await _service.ActivateRepositoryAsync("app-1", newRepo.Id);

        Assert.Equal("active", activated.Status);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.ActivateRepositoryAsync("app-1", "nope"));
    }

    [Fact]
    public async Task Update_ToActive_ConflictsWithTheOtherOfTheSameRole()
    {
        var a = await _service.CreateRepositoryAsync("app-1", Active("a", "contrato"));
        var b = await _service.CreateRepositoryAsync("app-1", Active("b", "contrato", status: "legacy"));

        await Assert.ThrowsAsync<DomainConflictException>(
            () => _service.UpdateRepositoryAsync("app-1", b.Id, Active("b", "contrato")));

        Assert.Equal("active", (await _service.ListRepositoriesAsync("app-1")).Single(r => r.Id == a.Id).Status);
    }

    [Fact]
    public async Task Update_OfTheActiveItself_DoesNotConflictWithItself()
    {
        var a = await _service.CreateRepositoryAsync("app-1", Active("a", "contrato"));

        var updated = await _service.UpdateRepositoryAsync("app-1", a.Id, Active("a-renomeado", "contrato"));

        Assert.Equal("a-renomeado", updated.Name);
        Assert.Equal("active", updated.Status);
    }

    [Fact]
    public async Task Update_ChangingTheRoleOfAnActive_ConflictsWhenThatRoleAlreadyHasOne()
    {
        await _service.CreateRepositoryAsync("app-1", Active("a", "aplicacao"));
        var infra = await _service.CreateRepositoryAsync("app-1", Active("b", "infra"));

        await Assert.ThrowsAsync<DomainConflictException>(
            () => _service.UpdateRepositoryAsync("app-1", infra.Id, Active("b", "aplicacao")));
    }

    [Fact]
    public async Task Delete_OfTheActive_PromotesNobody()
    {
        var active = await _service.CreateRepositoryAsync("app-1", Active("ativo", "aplicacao"));
        var legacy = await _service.CreateRepositoryAsync("app-1", Active("antigo", "aplicacao", status: "legacy"));

        await _service.DeleteRepositoryAsync("app-1", active.Id);

        Assert.Equal("legacy", (await _service.ListRepositoriesAsync("app-1")).Single(r => r.Id == legacy.Id).Status);
    }

    [Fact]
    public async Task SiglaApp_IsStoredUppercase_SiglaLowercase_AndConflictsIgnoreCase()
    {
        var repo = await _service.CreateRepositoryAsync("app-1", new("a", null, null, null, "AB1", "ab1-0001", null));

        Assert.Equal("AB1-0001", repo.SiglaApp);
        Assert.Equal("ab1", repo.Sigla);
        await Assert.ThrowsAsync<DomainConflictException>(
            () => _service.CreateRepositoryAsync("app-2", new("b", null, null, null, null, "Ab1-0001", null)));
    }
}
