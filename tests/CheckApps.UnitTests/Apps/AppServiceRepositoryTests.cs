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
}
