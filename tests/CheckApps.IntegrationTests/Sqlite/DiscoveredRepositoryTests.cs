using CheckApps.Application.Persistence;
using CheckApps.Domain;
using CheckApps.Domain.Discovery;
using CheckApps.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.IntegrationTests.Sqlite;

public class DiscoveredRepositoryTests(SqliteFixture fixture) : IClassFixture<SqliteFixture>
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SquadRepository _squads = new(fixture.ConnectionFactory);
    private readonly AppRepository _apps = new(fixture.ConnectionFactory);
    private readonly DiscoveredRepositoryRepository _discovered = new(fixture.ConnectionFactory);

    [Fact]
    public async Task Upsert_NewThenSameThenChanged_ReportsNewUnchangedUpdated()
    {
        var repo = NewDiscovered(sigla: Unique("s"));

        Assert.Equal(UpsertOutcome.New, await _discovered.UpsertAsync(repo));

        repo.LastSeenAt = T0.AddDays(1);
        Assert.Equal(UpsertOutcome.Unchanged, await _discovered.UpsertAsync(repo));

        repo.Archived = true;
        Assert.Equal(UpsertOutcome.Updated, await _discovered.UpsertAsync(repo));

        var stored = await _discovered.GetAsync(repo.Id);
        Assert.True(stored!.Archived);
        Assert.Equal(T0.AddDays(1), stored.LastSeenAt);
        Assert.Equal(T0, stored.FirstSeenAt);
    }

    [Fact]
    public async Task Upsert_NeverResetsTheTriageStatus()
    {
        var repo = NewDiscovered(sigla: Unique("s"));
        await _discovered.UpsertAsync(repo);
        await _discovered.SetStatusAsync(repo.Id, TriageStatus.Ignored);

        repo.LastSeenAt = T0.AddDays(2);
        repo.PushedAt = T0.AddDays(2);
        await _discovered.UpsertAsync(repo);

        var stored = await _discovered.GetAsync(repo.Id);
        Assert.Equal(TriageStatus.Ignored, stored!.Status);
        Assert.Equal(T0.AddDays(2), stored.PushedAt);
    }

    [Fact]
    public async Task Upsert_RenamedRepository_KeepsTheSameIdentity()
    {
        var sigla = Unique("s");
        var repo = NewDiscovered(sigla);
        await _discovered.UpsertAsync(repo);

        repo.Name = "lab-novo-nome";
        await _discovered.UpsertAsync(repo);

        var all = await _discovered.ListAsync(status: null, sigla);
        var only = Assert.Single(all);
        Assert.Equal(repo.Id, only.Id);
        Assert.Equal("lab-novo-nome", only.Name);
    }

    [Fact]
    public async Task List_FiltersByStatusAndSigla_AndOrdersBySiglaThenName()
    {
        var siglaA = Unique("a");
        var siglaB = Unique("b");
        var b2 = NewDiscovered(siglaB, name: "lab-b-2");
        var b1 = NewDiscovered(siglaB, name: "lab-b-1");
        var a1 = NewDiscovered(siglaA, name: "lab-a-1");
        foreach (var r in new[] { b2, b1, a1 })
        {
            await _discovered.UpsertAsync(r);
        }
        await _discovered.SetStatusAsync(b2.Id, TriageStatus.OtherSquad);

        var newOnes = await _discovered.ListAsync(TriageStatus.New, sigla: null);
        var onlyB = await _discovered.ListAsync(status: null, siglaB.ToUpperInvariant());
        var otherSquad = await _discovered.ListAsync(TriageStatus.OtherSquad, siglaB);

        Assert.Contains(newOnes, r => r.Id == a1.Id);
        Assert.Contains(newOnes, r => r.Id == b1.Id);
        Assert.DoesNotContain(newOnes, r => r.Id == b2.Id);
        Assert.Equal(["lab-b-1", "lab-b-2"], onlyB.Select(r => r.Name));
        Assert.Equal([b2.Id], otherSquad.Select(r => r.Id));
    }

    [Fact]
    public async Task List_ExcludesRepositoriesAlreadyInTheCatalog_AndIsInCatalogSaysSo()
    {
        var sigla = Unique("c");
        var repo = NewDiscovered(sigla);
        await _discovered.UpsertAsync(repo);
        Assert.False(await _discovered.IsInCatalogAsync(repo.Provider, repo.ExternalId));
        Assert.Single(await _discovered.ListAsync(status: null, sigla));

        var appId = await CreateAppAsync();
        await _apps.AddRepositoryAsync(new Repository
        {
            Id = Guid.NewGuid().ToString("n"),
            AppId = appId,
            Name = repo.Name,
            Provider = repo.Provider,
            ExternalId = repo.ExternalId
        });

        Assert.True(await _discovered.IsInCatalogAsync(repo.Provider, repo.ExternalId));
        Assert.Empty(await _discovered.ListAsync(status: null, sigla));
    }

    [Fact]
    public async Task Repository_RoundTripsSiglaSiglaAppRoleAndSource()
    {
        var appId = await CreateAppAsync();
        var repoId = Guid.NewGuid().ToString("n");

        await _apps.AddRepositoryAsync(new Repository
        {
            Id = repoId,
            AppId = appId,
            Name = "lab-ab1-app-pagamentos",
            Provider = "github",
            ExternalId = "123",
            Sigla = "ab1",
            SiglaApp = $"AB1-{Guid.NewGuid():N}",
            Role = RepositoryRoles.Application,
            Source = RepositorySources.GithubScan
        });

        var stored = Assert.Single(await _apps.GetRepositoriesAsync(appId));
        Assert.Equal("ab1", stored.Sigla);
        Assert.StartsWith("AB1-", stored.SiglaApp);
        Assert.Equal(RepositoryRoles.Application, stored.Role);
        Assert.Equal(RepositorySources.GithubScan, stored.Source);
    }

    [Fact]
    public async Task Repository_SiglaApp_IsUniqueWhenPresent_ButManyRepositoriesMayHaveNone()
    {
        var appId = await CreateAppAsync();
        var siglaApp = $"AB1-{Guid.NewGuid():N}";
        await _apps.AddRepositoryAsync(NewRepository(appId, siglaApp));

        await Assert.ThrowsAsync<SqliteException>(() => _apps.AddRepositoryAsync(NewRepository(appId, siglaApp)));

        await _apps.AddRepositoryAsync(NewRepository(appId, siglaApp: null));
        await _apps.AddRepositoryAsync(NewRepository(appId, siglaApp: null));
        Assert.Equal(3, (await _apps.GetRepositoriesAsync(appId)).Count);
    }

    private async Task<string> CreateAppAsync()
    {
        var squadId = Unique("sq");
        var appId = Unique("app");
        await _squads.SaveAsync(new Squad { Id = squadId, Name = "Squad Teste" });
        await _apps.SaveAsync(new App { Id = appId, SquadId = squadId, Name = "App Teste" });
        return appId;
    }

    private static string Unique(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..12];

    private static Repository NewRepository(string appId, string? siglaApp) => new()
    {
        Id = Guid.NewGuid().ToString("n"),
        AppId = appId,
        Name = "repo",
        SiglaApp = siglaApp
    };

    private static DiscoveredRepository NewDiscovered(string sigla, string? name = null)
    {
        var externalId = Guid.NewGuid().ToString("n");
        return new DiscoveredRepository
        {
            Id = DiscoveredRepository.IdFor("github", externalId),
            Provider = "github",
            ExternalId = externalId,
            Name = name ?? $"lab-{sigla}-app-x",
            Url = "https://example.invalid/x",
            Sigla = sigla,
            FirstSeenAt = T0,
            LastSeenAt = T0,
            Source = RepositorySources.GithubScan
        };
    }
}
