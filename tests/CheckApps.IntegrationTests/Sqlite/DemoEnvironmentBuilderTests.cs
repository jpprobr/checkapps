using CheckApps.Application.Analysis;
using CheckApps.Application.Checks;
using CheckApps.Application.DependencyInjection;
using CheckApps.Application.Journeys;
using CheckApps.Application.Persistence;
using CheckApps.Domain.QualityAssessment;
using CheckApps.Infrastructure.DependencyInjection;
using CheckApps.Infrastructure.Seeding;
using CheckApps.Infrastructure.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CheckApps.IntegrationTests.Sqlite;

/// <summary>
/// A base de demonstração completa (spec 023) sobre SQLite de verdade, com a mesma composição do seed e sem rede. O teste não liga o
/// modo demo (que apontaria para o demo.db da máquina): configura direto as fontes fake num banco temporário.
/// </summary>
public sealed class DemoEnvironmentBuilderTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), "checkapps-tests", $"demo-{Guid.NewGuid():N}.db");
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Sqlite:DatabasePath"] = _databasePath,
            ["Scan:Source"] = "fake",
            ["Scan:NameTemplate"] = "fake-{sigla}-"
        }).Build();

        var services = new ServiceCollection();
        services.AddCheckAppsInfrastructure(configuration);
        services.AddCheckAppsApplication();
        _provider = services.BuildServiceProvider();

        await _provider.GetRequiredService<SqliteSchemaBootstrapper>().EnsureSchemaAsync();
        await DemoDataSeeder.SeedAsync(
            new DemoDataFactory(_provider.GetRequiredService<IQualityAssessmentEngine>()).Build(),
            _provider.GetRequiredService<ISquadRepository>(), _provider.GetRequiredService<IAppRepository>(),
            _provider.GetRequiredService<IQualitySnapshotRepository>());

        using var scope = _provider.CreateScope();
        await ActivatorUtilities.CreateInstance<DemoEnvironmentBuilder>(scope.ServiceProvider).BuildAsync();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _databasePath, $"{_databasePath}-wal", $"{_databasePath}-shm" })
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // Arquivo temporário preso pelo SO não deve falhar a suíte.
            }
        }
    }

    private T Get<T>() where T : notnull => _provider.CreateScope().ServiceProvider.GetRequiredService<T>();

    [Fact]
    public async Task TheTriage_CreatesTheDemoApps_AndLinksTheContractRepository()
    {
        var apps = await Get<IAppRepository>().ListAllAsync();

        Assert.Contains(apps, a => a.Id == "pagamentos" && a.SquadId == "sq-1");
        Assert.Contains(apps, a => a.Id == "bff-pagamentos");
        Assert.Contains(await Get<IAppRepository>().GetRepositoriesAsync("pagamentos"), r => r.Role == "contrato");
    }

    [Fact]
    public async Task TheContract_HasAComparisonBetweenTwoCommits_WithBreakingChanges()
    {
        var changes = await Get<ContractService>().GetChangesForAppAsync("pagamentos");

        var contract = Assert.Single(changes.Repositories, r => r.State == ContractChangeStates.Compared);
        Assert.NotEqual(contract.Baseline!.CommitSha, contract.Analysis!.CommitSha);
        Assert.Contains(contract.Changes, c => c.Severity == ContractChangeSeverities.Breaking);
    }

    [Fact]
    public async Task TheJourneys_FormATree_WithStepsAndImpact()
    {
        var service = Get<JourneyService>();
        var journeys = await service.ListAsync();

        Assert.Equal(4, journeys.Count);
        var platform = Assert.Single(journeys, j => j.Journey.ParentId is null);
        Assert.Equal("plataforma", platform.Journey.Level);

        var pay = Assert.Single(journeys, j => j.Journey.Name == "Simular e pagar");
        Assert.Equal(4, pay.StepCount);

        var impact = await service.GetImpactAsync(platform.Journey.Id);
        Assert.True(impact.Breaking > 0);
        Assert.Equal(0, impact.StepsWithoutComparison);

        var detail = await service.GetDetailAsync(pay.Journey.Id);
        Assert.Contains(detail.Steps, s => s.Resolution == StepResolutions.Cataloged);
        Assert.Contains(detail.Steps, s => s.Resolution == StepResolutions.OutsideContract);   // /relatorios saiu na revisão 1
    }

    [Fact]
    public async Task TheCheck_IncludesEveryApp_WithVariedStatuses()
    {
        var checks = Get<CheckService>();
        var summary = Assert.Single(await checks.ListAsync(includeClosed: false));
        var details = (await checks.GetAsync(summary.Check.Id))!;

        Assert.Equal((await Get<IAppRepository>().ListAllAsync()).Count, details.Items.Count);
        Assert.True(details.Items.Select(i => i.Status).Distinct().Count() >= 4);
    }

    [Fact]
    public async Task TheDiscovery_FindsTheDemoRepositoriesOfBothSiglas()
    {
        var discovered = await Get<IDiscoveredRepositoryRepository>().ListAsync(null, null, CatalogScope.Any);

        Assert.Contains(discovered, d => d.Sigla == "ab1");
        Assert.Contains(discovered, d => d.Sigla == "cd2");
    }
}
