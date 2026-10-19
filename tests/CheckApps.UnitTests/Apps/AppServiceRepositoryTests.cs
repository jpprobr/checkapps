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
        _service = new AppService(_apps, new FakeSquadRepository(), new FakeQualitySnapshotRepository(), new FakeCheckRepository());
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
    public async Task Create_AsActive_DemotesOnlyTheOtherActiveOfTheSameRole()
    {
        var infra = await _service.CreateRepositoryAsync("app-1", Active("infra-1", "infra"));
        var app1 = await _service.CreateRepositoryAsync("app-1", Active("app-1", "aplicacao"));

        var app2 = await _service.CreateRepositoryAsync("app-1", Active("app-2", "aplicacao"));

        var all = await _service.ListRepositoriesAsync("app-1");
        Assert.Equal("legacy", all.Single(r => r.Id == app1.Id).Status);
        Assert.Equal("active", all.Single(r => r.Id == app2.Id).Status);
        Assert.Equal("active", all.Single(r => r.Id == infra.Id).Status);   // outro papel: continua ativo
    }

    [Fact]
    public async Task Create_ForAnotherApp_OrAsLegacyOrArchived_DemotesNobody()
    {
        var first = await _service.CreateRepositoryAsync("app-1", Active("a", "aplicacao"));
        await _service.CreateRepositoryAsync("app-2", Active("b", "aplicacao"));
        await _service.CreateRepositoryAsync("app-1", Active("c", "aplicacao", status: "legacy"));
        await _service.CreateRepositoryAsync("app-1", Active("d", "aplicacao", status: "archived"));

        Assert.Equal("active", (await _service.ListRepositoriesAsync("app-1")).Single(r => r.Id == first.Id).Status);
    }

    [Fact]
    public async Task NoRole_IsOneGroupOnly_AndRoleComparisonIgnoresCase()
    {
        var a = await _service.CreateRepositoryAsync("app-1", Active("a", null));
        var b = await _service.CreateRepositoryAsync("app-1", Active("b", null));
        var c = await _service.CreateRepositoryAsync("app-1", Active("c", "Infra"));
        var d = await _service.CreateRepositoryAsync("app-1", Active("d", "infra"));

        var all = await _service.ListRepositoriesAsync("app-1");
        Assert.Equal("legacy", all.Single(r => r.Id == a.Id).Status);
        Assert.Equal("active", all.Single(r => r.Id == b.Id).Status);
        Assert.Equal("legacy", all.Single(r => r.Id == c.Id).Status);
        Assert.Equal("active", all.Single(r => r.Id == d.Id).Status);
    }

    [Fact]
    public async Task Activate_SwapsTheActiveOfTheGroup_AndReportsWhoWasDemoted()
    {
        var oldRepo = await _service.CreateRepositoryAsync("app-1", Active("velho", "aplicacao"));
        var newRepo = await _service.CreateRepositoryAsync("app-1", Active("novo", "aplicacao", status: "legacy"));

        var result = await _service.ActivateRepositoryAsync("app-1", newRepo.Id);

        Assert.Equal("active", result.Repository.Status);
        Assert.Equal(["velho"], result.Demoted);
        Assert.Equal("legacy", (await _service.ListRepositoriesAsync("app-1")).Single(r => r.Id == oldRepo.Id).Status);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.ActivateRepositoryAsync("app-1", "nope"));
    }

    [Fact]
    public async Task Update_ToActive_DemotesTheOtherOfTheSameRole()
    {
        var a = await _service.CreateRepositoryAsync("app-1", Active("a", "contrato"));
        var b = await _service.CreateRepositoryAsync("app-1", Active("b", "contrato", status: "legacy"));

        await _service.UpdateRepositoryAsync("app-1", b.Id, Active("b", "contrato"));

        Assert.Equal("legacy", (await _service.ListRepositoriesAsync("app-1")).Single(r => r.Id == a.Id).Status);
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
