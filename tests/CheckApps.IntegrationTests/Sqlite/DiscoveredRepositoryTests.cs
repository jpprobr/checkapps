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
        Assert.False(await _discovered.IsInCatalogAsync(repo));
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

        Assert.True(await _discovered.IsInCatalogAsync(repo));
        Assert.Empty(await _discovered.ListAsync(status: null, sigla));
    }

    [Fact]
    public async Task List_ByCatalogScope_PendingCatalogedAndAny_CombinedWithStatusAndSigla()
    {
        var sigla = Unique("k");
        var pending = NewDiscovered(sigla, name: "lab-k-pendente");
        var cataloged = NewDiscovered(sigla, name: "lab-k-catalogado");
        var ignored = NewDiscovered(sigla, name: "lab-k-ignorado");
        foreach (var r in new[] { pending, cataloged, ignored })
        {
            await _discovered.UpsertAsync(r);
        }
        await _discovered.SetStatusAsync(ignored.Id, TriageStatus.Ignored);
        var appId = await CreateAppAsync();
        await _apps.AddRepositoryAsync(new Repository
        {
            Id = Guid.NewGuid().ToString("n"), AppId = appId, Name = cataloged.Name,
            Provider = cataloged.Provider, ExternalId = cataloged.ExternalId
        });

        var byDefault = await _discovered.ListAsync(status: null, sigla);
        var explicitPending = await _discovered.ListAsync(status: null, sigla, CatalogScope.Pending);
        var onlyCataloged = await _discovered.ListAsync(status: null, sigla, CatalogScope.Cataloged);
        var any = await _discovered.ListAsync(status: null, sigla, CatalogScope.Any);
        var anyIgnored = await _discovered.ListAsync(TriageStatus.Ignored, sigla, CatalogScope.Any);
        var catalogedNew = await _discovered.ListAsync(TriageStatus.New, sigla, CatalogScope.Cataloged);

        Assert.Equal(["lab-k-ignorado", "lab-k-pendente"], byDefault.Select(r => r.Name));   // o comportamento original
        Assert.Equal(byDefault.Select(r => r.Id), explicitPending.Select(r => r.Id));
        Assert.Equal(["lab-k-catalogado"], onlyCataloged.Select(r => r.Name));
        Assert.Equal(["lab-k-catalogado", "lab-k-ignorado", "lab-k-pendente"], any.Select(r => r.Name));
        Assert.Equal(["lab-k-ignorado"], anyIgnored.Select(r => r.Name));
        Assert.Equal(["lab-k-catalogado"], catalogedNew.Select(r => r.Name));   // o status armazenado do catalogado continua "new"
    }

    [Fact]
    public async Task ListCatalogOnly_ReturnsManualRepositories_ButNotTheOnesTheInboxAlreadyShows()
    {
        var sigla = Unique("c");
        var scanned = NewDiscovered(sigla, name: "lab-c-scan");
        await _discovered.UpsertAsync(scanned);
        var appId = await CreateAppAsync();
        await _apps.AddRepositoryAsync(new Repository
        {
            Id = Guid.NewGuid().ToString("n"), AppId = appId, Name = scanned.Name, Sigla = sigla,
            Provider = scanned.Provider, ExternalId = scanned.ExternalId, Source = RepositorySources.GithubScan
        });
        await _apps.AddRepositoryAsync(new Repository
        {
            Id = Guid.NewGuid().ToString("n"), AppId = appId, Name = "lab-c-manual", Sigla = sigla,
            Source = RepositorySources.Manual
        });
        await _apps.AddRepositoryAsync(new Repository
        {
            Id = Guid.NewGuid().ToString("n"), AppId = appId, Name = "lab-c-outra-sigla", Sigla = Unique("x")
        });

        var items = await _apps.ListCatalogOnlyRepositoriesAsync(sigla);

        var item = Assert.Single(items);   // o do scan sai (já está na caixa de entrada); o de outra sigla é filtrado
        Assert.Equal("lab-c-manual", item.Repository.Name);
        Assert.Equal(appId, item.AppId);
        Assert.False(string.IsNullOrEmpty(item.AppName));
        Assert.Equal("Squad Teste", item.SquadName);
        Assert.Equal((await _apps.GetAsync(appId))!.SquadId, item.SquadId);
        Assert.Contains(await _apps.ListCatalogOnlyRepositoriesAsync(null), i => i.Repository.Name == "lab-c-outra-sigla");
    }

    [Fact]
    public async Task List_WhenTheAppIsRemoved_TheRepositoryReturnsToPending()
    {
        var sigla = Unique("r");
        var repo = NewDiscovered(sigla);
        await _discovered.UpsertAsync(repo);
        var appId = await CreateAppAsync();
        await _apps.AddRepositoryAsync(new Repository
        {
            Id = Guid.NewGuid().ToString("n"), AppId = appId, Name = repo.Name, Provider = repo.Provider, ExternalId = repo.ExternalId
        });
        Assert.Empty(await _discovered.ListAsync(status: null, sigla));

        await _apps.DeleteAllAsync(appId);   // os repositórios do app saem junto (cascata)

        Assert.Single(await _discovered.ListAsync(status: null, sigla));
        Assert.Empty(await _discovered.ListAsync(status: null, sigla, CatalogScope.Cataloged));
    }

    [Fact]
    public async Task GetCatalogApps_ReturnsTheAppAndItsName_OnlyForLinkedRepositories_InOneCall()
    {
        var appId = await CreateAppAsync();
        var linked = Guid.NewGuid().ToString("n");
        var otherLinked = Guid.NewGuid().ToString("n");
        var notLinked = Guid.NewGuid().ToString("n");
        await _apps.AddRepositoryAsync(new Repository { Id = Guid.NewGuid().ToString("n"), AppId = appId, Name = "a", Provider = "github", ExternalId = linked });
        await _apps.AddRepositoryAsync(new Repository { Id = Guid.NewGuid().ToString("n"), AppId = appId, Name = "b", Provider = "github", ExternalId = otherLinked });
        // mesmo id externo em OUTRO provedor não conta como "no catálogo" deste
        await _apps.AddRepositoryAsync(new Repository { Id = Guid.NewGuid().ToString("n"), AppId = appId, Name = "c", Provider = "outro", ExternalId = notLinked });

        var dLinked = NewDiscovered("m", externalId: linked);
        var dOtherLinked = NewDiscovered("m", externalId: otherLinked);
        var dNotLinked = NewDiscovered("m", externalId: notLinked);

        var map = await _discovered.GetCatalogAppsAsync([dLinked, dOtherLinked, dNotLinked]);

        Assert.Equal(2, map.Count);
        Assert.Equal(appId, map[dLinked.Id].AppId);
        Assert.Equal("App Teste", map[dLinked.Id].AppName);
        Assert.Equal("Squad Teste", map[dLinked.Id].SquadName);
        Assert.Equal((await _apps.GetAsync(appId))!.SquadId, map[dLinked.Id].SquadId);
        Assert.Equal(appId, map[dOtherLinked.Id].AppId);
        Assert.False(map.ContainsKey(dNotLinked.Id));
    }

    [Fact]
    public async Task GetCatalogApps_WithNoIds_IsEmpty_WithoutQuerying()
    {
        var map = await _discovered.GetCatalogAppsAsync([]);

        Assert.Empty(map);
    }

    [Fact]
    public async Task GetCatalogApps_AgreesWithIsInCatalog()
    {
        var appId = await CreateAppAsync();
        var externalId = Guid.NewGuid().ToString("n");
        var discovered = NewDiscovered("m", externalId: externalId);
        Assert.Empty(await _discovered.GetCatalogAppsAsync([discovered]));
        Assert.False(await _discovered.IsInCatalogAsync(discovered));

        await _apps.AddRepositoryAsync(new Repository { Id = Guid.NewGuid().ToString("n"), AppId = appId, Name = "x", Provider = "github", ExternalId = externalId });

        Assert.Single(await _discovered.GetCatalogAppsAsync([discovered]));
        Assert.True(await _discovered.IsInCatalogAsync(discovered));
    }

    [Fact]
    public async Task CatalogMatch_ByUrl_IgnoresCaseAndTrailingSlash_InEveryQuery()
    {
        var sigla = Unique("u");
        var repo = NewDiscovered(sigla, url: "https://github.com/Org/Repo-X");
        await _discovered.UpsertAsync(repo);
        var appId = await CreateAppAsync();
        // cadastro manual: sem provedor nem id externo, só a URL
        await _apps.AddRepositoryAsync(new Repository
        {
            Id = Guid.NewGuid().ToString("n"), AppId = appId, Name = "manual", Sigla = sigla,
            Url = " https://github.com/org/repo-x/ ", Source = RepositorySources.Manual
        });

        Assert.True(await _discovered.IsInCatalogAsync(repo));
        Assert.Empty(await _discovered.ListAsync(status: null, sigla));                                   // sai de "pendentes"
        Assert.Single(await _discovered.ListAsync(status: null, sigla, CatalogScope.Cataloged));         // e entra em "no catálogo"
        var map = await _discovered.GetCatalogAppsAsync([repo]);
        Assert.Equal(appId, map[repo.Id].AppId);
        // o cadastro manual não aparece duas vezes: o descoberto já o representa
        Assert.DoesNotContain(await _apps.ListCatalogOnlyRepositoriesAsync(sigla), i => i.Repository.Name == "manual");
    }

    [Fact]
    public async Task CatalogMatch_ByUrl_DoesNotMatchDifferentUrls_OrEmptyOnes()
    {
        var sigla = Unique("v");
        var repo = NewDiscovered(sigla, url: "https://github.com/org/um");
        await _discovered.UpsertAsync(repo);
        var appId = await CreateAppAsync();
        await _apps.AddRepositoryAsync(new Repository
        {
            Id = Guid.NewGuid().ToString("n"), AppId = appId, Name = "outro", Sigla = sigla, Url = "https://github.com/org/dois"
        });
        await _apps.AddRepositoryAsync(new Repository
        {
            Id = Guid.NewGuid().ToString("n"), AppId = appId, Name = "sem-url", Sigla = sigla, Url = "  "
        });

        Assert.False(await _discovered.IsInCatalogAsync(repo));
        Assert.Single(await _discovered.ListAsync(status: null, sigla));
        Assert.Empty(await _discovered.GetCatalogAppsAsync([repo]));
    }

    [Fact]
    public async Task IsSiglaAppInUse_IgnoresCase_ForBothRepositories()
    {
        var appId = await CreateAppAsync();
        var siglaApp = $"ab{Guid.NewGuid():N}"[..8];
        await _apps.AddRepositoryAsync(new Repository
        {
            Id = Guid.NewGuid().ToString("n"), AppId = appId, Name = "x", SiglaApp = siglaApp.ToUpperInvariant()
        });

        Assert.True(await _discovered.IsSiglaAppInUseAsync(siglaApp.ToLowerInvariant()));
        Assert.True(await _apps.IsSiglaAppInUseAsync(siglaApp.ToLowerInvariant(), null));
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

    private static DiscoveredRepository NewDiscovered(
        string sigla, string? name = null, string? externalId = null, string? url = null)
    {
        externalId ??= Guid.NewGuid().ToString("n");
        return new DiscoveredRepository
        {
            Id = DiscoveredRepository.IdFor("github", externalId),
            Provider = "github",
            ExternalId = externalId,
            Name = name ?? $"lab-{sigla}-app-x",
            Url = url ?? $"https://example.invalid/{externalId}",
            Sigla = sigla,
            FirstSeenAt = T0,
            LastSeenAt = T0,
            Source = RepositorySources.GithubScan
        };
    }
}
