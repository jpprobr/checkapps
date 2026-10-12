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
        string externalId, string name, string sigla = "ab1", string? siglaApp = null, bool archived = false)
    {
        var repository = new DiscoveredRepository
        {
            Id = DiscoveredRepository.IdFor("github", externalId),
            Provider = "github",
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
}
