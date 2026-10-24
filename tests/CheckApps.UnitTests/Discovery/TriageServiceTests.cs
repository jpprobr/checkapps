using CheckApps.Application;
using CheckApps.Application.Apps;
using CheckApps.Application.Discovery;
using CheckApps.Application.Persistence;
using CheckApps.Domain;
using CheckApps.Domain.Discovery;
using CheckApps.UnitTests.TestDoubles;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.UnitTests.Discovery;

public class TriageServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeAppRepository _apps = new();
    private readonly FakeSquadRepository _squads = new();
    private readonly FakeDiscoveredRepositoryRepository _discovered;
    private readonly TriageService _sut;

    public TriageServiceTests()
    {
        _discovered = new FakeDiscoveredRepositoryRepository(_apps);
        var appService = new AppService(_apps, _squads, new FakeQualitySnapshotRepository(), new FakeCheckRepository());
        _sut = new TriageService(_discovered, _apps, _squads, appService, new ScanOptions { NameTemplate = "lab-{sigla}-" });
        _squads.SaveAsync(new Squad { Id = "sq-1", Name = "Squad" }).Wait();
    }

    private async Task<DiscoveredRepository> SeedAsync(
        string externalId, string name, string sigla = "ab1", string? siglaApp = null, bool archived = false,
        string provider = "github")
    {
        var repository = new DiscoveredRepository
        {
            Id = DiscoveredRepository.IdFor(provider, externalId),
            Provider = provider,
            ExternalId = externalId,
            Name = name,
            Url = "https://example.invalid/" + name,
            Sigla = sigla,
            SiglaApp = siglaApp,
            Archived = archived,
            FirstSeenAt = T0,
            LastSeenAt = T0,
            Source = RepositorySources.GithubScan
        };
        await _discovered.UpsertAsync(repository);
        return repository;
    }

    [Fact]
    public async Task ListAsync_ReturnsTheSuggestionComputedAtReadTime()
    {
        await SeedAsync("1", "lab-ab1-app-pagamentos", siglaApp: "AB1-0001");
        await SeedAsync("2", "lab-ab1-infra-fila");

        var items = await _sut.ListAsync(TriageStatus.New, "ab1");

        var app = items.Single(i => i.Repository.ExternalId == "1");
        Assert.Equal(RepositoryRoles.Application, app.Suggestion.Role);
        Assert.Equal("Pagamentos", app.Suggestion.Name);
        Assert.Equal(SuggestionConfidence.High, app.Suggestion.Confidence);
        Assert.Equal(RepositoryRoles.Infra, items.Single(i => i.Repository.ExternalId == "2").Suggestion.Role);
    }

    [Fact]
    public async Task ListAsync_ByDefault_ExcludesThoseAlreadyInTheCatalog_AndCarriesNoCatalogApp()
    {
        await SeedAsync("1", "lab-ab1-app-pagamentos");
        await SeedAsync("2", "lab-ab1-infra-fila");
        await _sut.CreateAppAsync("github:1", "Pagamentos", "sq-1", null, null);

        var items = await _sut.ListAsync(null, null);

        var only = Assert.Single(items);
        Assert.Equal("2", only.Repository.ExternalId);
        Assert.Null(only.CatalogApp);
    }

    [Fact]
    public async Task ListAsync_Cataloged_ReturnsOnlyThoseInTheCatalog_WithTheirApp()
    {
        await SeedAsync("1", "lab-ab1-app-pagamentos");
        await SeedAsync("2", "lab-ab1-infra-fila");
        await _sut.CreateAppAsync("github:1", "Pagamentos", "sq-1", null, null);

        var item = Assert.Single(await _sut.ListAsync(null, null, CatalogScope.Cataloged));

        Assert.Equal("1", item.Repository.ExternalId);
        Assert.Equal("pagamentos", item.CatalogApp!.AppId);
        Assert.Equal("Pagamentos", item.CatalogApp.AppName);
    }

    [Fact]
    public async Task ListAsync_Any_ReturnsEverything_EachWithItsOwnState()
    {
        await SeedAsync("1", "lab-ab1-app-pagamentos");
        await SeedAsync("2", "lab-ab1-infra-fila");
        await SeedAsync("3", "lab-ab1-contrato-api-pagamentos");
        await _sut.CreateAppAsync("github:1", "Pagamentos", "sq-1", null, null);
        await _sut.IgnoreAsync("github:3");

        var items = await _sut.ListAsync(null, null, CatalogScope.Any);

        Assert.Equal(3, items.Count);
        Assert.Equal("pagamentos", items.Single(i => i.Repository.ExternalId == "1").CatalogApp!.AppId);
        Assert.Null(items.Single(i => i.Repository.ExternalId == "2").CatalogApp);
        Assert.Equal(TriageStatus.Ignored, items.Single(i => i.Repository.ExternalId == "3").Repository.Status);
        Assert.Null(items.Single(i => i.Repository.ExternalId == "3").CatalogApp);
    }

    [Fact]
    public async Task ListAsync_Any_ResolvesTheAppPerProvider_ForTheSameExternalId()
    {
        // fake e github podem ter o mesmo id externo; só o do provedor vinculado está no catálogo
        await SeedAsync("7", "lab-ab1-app-um", provider: "github");
        await SeedAsync("7", "lab-ab1-app-um-fake", provider: "fake");
        _apps.Add(new App { Id = "um", SquadId = "sq-1", Name = "Um" });
        await _apps.AddRepositoryAsync(new Repository { Id = "r1", AppId = "um", Name = "x", Provider = "fake", ExternalId = "7" });

        var items = await _sut.ListAsync(null, null, CatalogScope.Any);

        Assert.Equal("Um", items.Single(i => i.Repository.Provider == "fake").CatalogApp!.AppName);
        Assert.Null(items.Single(i => i.Repository.Provider == "github").CatalogApp);
    }

    [Fact]
    public async Task ListAsync_WithoutNameTemplate_StillWorks_WithoutRoleSuggestions()
    {
        await SeedAsync("1", "lab-ab1-app-pagamentos");
        var appService = new AppService(_apps, _squads, new FakeQualitySnapshotRepository(), new FakeCheckRepository());
        var sut = new TriageService(_discovered, _apps, _squads, appService, new ScanOptions());

        var item = Assert.Single(await sut.ListAsync(null, null));

        Assert.Null(item.Suggestion.Role);
        Assert.Equal(SuggestionConfidence.Low, item.Suggestion.Confidence);
    }

    [Fact]
    public async Task CreateAppAsync_CreatesTheAppAndLinksTheRepository_AndLeavesTheInbox()
    {
        await SeedAsync("1", "lab-ab1-app-pagamentos", siglaApp: "AB1-0001");

        var app = await _sut.CreateAppAsync("github:1", "Pagamentos", "sq-1", "Cobrança", role: null);

        Assert.Equal("pagamentos", app.Id);
        Assert.Equal("sq-1", app.SquadId);
        var repository = Assert.Single(await _apps.GetRepositoriesAsync(app.Id));
        Assert.Equal("lab-ab1-app-pagamentos", repository.Name);
        Assert.Equal("github", repository.Provider);
        Assert.Equal("1", repository.ExternalId);
        Assert.Equal("ab1", repository.Sigla);
        Assert.Equal("AB1-0001", repository.SiglaApp);
        Assert.Equal(RepositoryRoles.Application, repository.Role);   // vem da sugestão quando não informado
        Assert.Equal(RepositorySources.GithubScan, repository.Source);
        Assert.Equal("active", repository.Status);
        Assert.Empty(await _sut.ListAsync(TriageStatus.New, null));
    }

    [Fact]
    public async Task CreateAppAsync_UsesTheRoleTheUserChose_AndMarksArchivedRepositories()
    {
        await SeedAsync("1", "lab-ab1-app-legado", archived: true);

        var app = await _sut.CreateAppAsync("github:1", "Legado", "sq-1", null, role: "infra");

        var repository = Assert.Single(await _apps.GetRepositoriesAsync(app.Id));
        Assert.Equal("infra", repository.Role);
        Assert.Equal("archived", repository.Status);
    }

    [Fact]
    public async Task CreateAppAsync_Twice_ConflictsBecauseTheRepositoryIsAlreadyInTheCatalog()
    {
        await SeedAsync("1", "lab-ab1-app-pagamentos");
        await _sut.CreateAppAsync("github:1", "Pagamentos", "sq-1", null, null);

        await Assert.ThrowsAsync<DomainConflictException>(() =>
            _sut.CreateAppAsync("github:1", "Outro Nome", "sq-1", null, null));
    }

    [Theory]
    [InlineData("", "sq-1")]
    [InlineData("  ", "sq-1")]
    [InlineData("Nome", "nao-existe")]
    public async Task CreateAppAsync_RejectsBlankNameOrUnknownSquad_WithoutCreatingAnything(string name, string squadId)
    {
        await SeedAsync("1", "lab-ab1-app-pagamentos");

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateAppAsync("github:1", name, squadId, null, null));

        Assert.Empty(await _apps.ListBySquadAsync("sq-1"));
        Assert.Empty(_apps.AllRepositories);
    }

    [Fact]
    public async Task CreateAppAsync_WhenTheSiglaAppBelongsToAnotherRepository_ConflictsBeforeCreatingTheApp()
    {
        _apps.Add(new App { Id = "existente", SquadId = "sq-1", Name = "Existente" });
        await _apps.AddRepositoryAsync(new Repository
        {
            Id = "r-1", AppId = "existente", Name = "outro-repo", Provider = "github", ExternalId = "99", SiglaApp = "AB1-0001"
        });
        await SeedAsync("1", "lab-ab1-app-pagamentos", siglaApp: "AB1-0001");

        await Assert.ThrowsAsync<DomainConflictException>(() =>
            _sut.CreateAppAsync("github:1", "Pagamentos", "sq-1", null, null));

        Assert.Single(await _apps.ListBySquadAsync("sq-1"));   // só o que já existia: nada de app sem repositório
    }

    [Fact]
    public async Task CreateAppAsync_UnknownDiscovered_KeyNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.CreateAppAsync("github:404", "X", "sq-1", null, null));
    }

    [Fact]
    public async Task LinkAsync_AddsTheRepositoryToTheApp_WithoutWarningWhenTheSiglaMatches()
    {
        _apps.Add(new App { Id = "pagamentos", SquadId = "sq-1", Name = "Pagamentos" });
        await _apps.AddRepositoryAsync(new Repository { Id = "r-0", AppId = "pagamentos", Name = "antigo", Sigla = "ab1" });
        await SeedAsync("1", "lab-ab1-infra-fila");

        var warnings = await _sut.LinkAsync("github:1", "pagamentos", role: null);

        Assert.Empty(warnings);
        var repositories = await _apps.GetRepositoriesAsync("pagamentos");
        Assert.Equal(2, repositories.Count);
        Assert.Equal(RepositoryRoles.Infra, repositories.Single(r => r.ExternalId == "1").Role);
    }

    [Fact]
    public async Task LinkAsync_WarnsButDoesNotBlock_WhenTheSiglaDiffersFromTheAppsOtherRepositories()
    {
        _apps.Add(new App { Id = "pagamentos", SquadId = "sq-1", Name = "Pagamentos" });
        await _apps.AddRepositoryAsync(new Repository { Id = "r-0", AppId = "pagamentos", Name = "antigo", Sigla = "cd2" });
        await SeedAsync("1", "lab-ab1-app-novo", siglaApp: "AB1-0005");

        var warnings = await _sut.LinkAsync("github:1", "pagamentos", role: null);

        var warning = Assert.Single(warnings);
        Assert.Contains("cd2", warning);
        Assert.Contains("'ab1'", warning);
        Assert.Equal(2, (await _apps.GetRepositoriesAsync("pagamentos")).Count);
    }

    [Fact]
    public async Task LinkAsync_UnknownApp_IsInvalidInput_AndAlreadyInCatalogConflicts()
    {
        _apps.Add(new App { Id = "pagamentos", SquadId = "sq-1", Name = "Pagamentos" });
        await SeedAsync("1", "lab-ab1-app-x");

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.LinkAsync("github:1", "nao-existe", null));

        await _sut.LinkAsync("github:1", "pagamentos", null);
        await Assert.ThrowsAsync<DomainConflictException>(() => _sut.LinkAsync("github:1", "pagamentos", null));
    }

    [Fact]
    public async Task OtherSquadIgnoreAndRestore_MoveTheStatus_AndLeaveTheNewList()
    {
        await SeedAsync("1", "lab-ab1-app-x");

        await _sut.MarkOtherSquadAsync("github:1");
        Assert.Empty(await _sut.ListAsync(TriageStatus.New, null));
        Assert.Single(await _sut.ListAsync(TriageStatus.OtherSquad, null));

        await _sut.IgnoreAsync("github:1");
        Assert.Single(await _sut.ListAsync(TriageStatus.Ignored, null));

        await _sut.RestoreAsync("github:1");
        Assert.Single(await _sut.ListAsync(TriageStatus.New, null));
    }

    [Fact]
    public async Task StatusActions_OnAnUnknownOrAlreadyCatalogedRepository_FailAccordingly()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.IgnoreAsync("github:404"));

        await SeedAsync("1", "lab-ab1-app-x");
        await _sut.CreateAppAsync("github:1", "X", "sq-1", null, null);
        await Assert.ThrowsAsync<DomainConflictException>(() => _sut.IgnoreAsync("github:1"));
    }

    [Fact]
    public async Task LinkAsync_ConflictsWhenTheAppAlreadyHasAnActiveOfTheSameRole_AndChangesNothing()
    {
        _apps.Add(new App { Id = "pagamentos", SquadId = "sq-1", Name = "Pagamentos" });
        await _apps.AddRepositoryAsync(new Repository
        {
            Id = "r-0", AppId = "pagamentos", Name = "app-antigo", Sigla = "ab1", Role = RepositoryRoles.Application, Status = "active"
        });
        await _apps.AddRepositoryAsync(new Repository
        {
            Id = "r-1", AppId = "pagamentos", Name = "infra-fila", Sigla = "ab1", Role = RepositoryRoles.Infra, Status = "active"
        });
        await SeedAsync("1", "lab-ab1-app-novo");

        var ex = await Assert.ThrowsAsync<DomainConflictException>(
            () => _sut.LinkAsync("github:1", "pagamentos", role: null));

        Assert.Contains("app-antigo", ex.Message);
        var repositories = await _apps.GetRepositoriesAsync("pagamentos");
        Assert.Equal(2, repositories.Count);   // o descoberto não foi vinculado
        Assert.All(repositories, r => Assert.Equal("active", r.Status));
    }

    [Fact]
    public async Task LinkAsync_LinksAsActive_WhenTheOtherActiveHasAnotherRole()
    {
        _apps.Add(new App { Id = "pagamentos", SquadId = "sq-1", Name = "Pagamentos" });
        await _apps.AddRepositoryAsync(new Repository
        {
            Id = "r-1", AppId = "pagamentos", Name = "infra-fila", Sigla = "ab1", Role = RepositoryRoles.Infra, Status = "active"
        });
        await SeedAsync("1", "lab-ab1-app-novo");

        await _sut.LinkAsync("github:1", "pagamentos", role: RepositoryRoles.Application);

        var repositories = await _apps.GetRepositoriesAsync("pagamentos");
        Assert.Equal("active", repositories.Single(r => r.Id == "r-1").Status);
        Assert.Equal("active", repositories.Single(r => r.ExternalId == "1").Status);
    }

    [Fact]
    public async Task LinkAsync_StoresTheSiglaAppUppercase()
    {
        _apps.Add(new App { Id = "pagamentos", SquadId = "sq-1", Name = "Pagamentos" });
        await SeedAsync("1", "lab-ab1-app-x", siglaApp: "ab1-0001");

        await _sut.LinkAsync("github:1", "pagamentos", role: null);

        Assert.Equal("AB1-0001", (await _apps.GetRepositoriesAsync("pagamentos")).Single().SiglaApp);
    }

    [Fact]
    public async Task ListAndTriage_TreatARepositoryRegisteredByHandWithTheSameUrlAsInTheCatalog()
    {
        _apps.Add(new App { Id = "pagamentos", SquadId = "sq-1", Name = "Pagamentos" });
        var discovered = await SeedAsync("1", "lab-ab1-app-x");
        await _apps.AddRepositoryAsync(new Repository
        {
            Id = "manual-1", AppId = "pagamentos", Name = "x", Url = discovered.Url!.ToUpperInvariant() + "/", Source = RepositorySources.Manual
        });

        var items = await _sut.ListAsync(status: null, sigla: null, CatalogScope.Any);

        Assert.Equal("pagamentos", items.Single().CatalogApp!.AppId);
        Assert.Empty(await _sut.ListAsync(TriageStatus.New, null));   // não aparece como "novo"
        await Assert.ThrowsAsync<DomainConflictException>(() => _sut.LinkAsync("github:1", "pagamentos", role: null));
    }
}
