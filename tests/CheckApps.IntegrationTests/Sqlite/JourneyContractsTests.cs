using CheckApps.Application;
using CheckApps.Application.Analysis;
using CheckApps.Application.Apps;
using CheckApps.Application.Discovery;
using CheckApps.Application.Journeys;
using CheckApps.Domain;
using CheckApps.Domain.Discovery;
using CheckApps.Infrastructure.Analysis;
using CheckApps.Infrastructure.Analysis.OpenApi;
using CheckApps.Infrastructure.Sqlite;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.IntegrationTests.Sqlite;

/// <summary>
/// As jornadas (spec 022) de ponta a ponta com os contratos (spec 021), sobre SQLite de verdade: a análise lê as duas revisões do contrato
/// de demonstração, e o impacto de uma jornada sai das mudanças reais. Sem rede.
/// </summary>
public class JourneyContractsTests(SqliteFixture fixture) : IClassFixture<SqliteFixture>
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SquadRepository _squads = new(fixture.ConnectionFactory);
    private readonly AppRepository _apps = new(fixture.ConnectionFactory);
    private readonly AnalysisRepository _analyses = new(fixture.ConnectionFactory, TestAnalysisWriters.All);
    private readonly JourneyRepository _journeys = new(fixture.ConnectionFactory);

    private JourneyService Service() => new(_journeys, _squads, _apps, new ContractService(_apps, _analyses), TimeProvider.System);

    private async Task<(string AppId, string Sigla)> SeedContractAppAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var squadId = $"sq-{suffix}";
        var appId = $"app-{suffix}";
        var sigla = $"z{suffix[..2]}";
        await _squads.SaveAsync(new Squad { Id = squadId, Name = $"Squad {suffix}" });
        await _apps.SaveAsync(new App { Id = appId, SquadId = squadId, Name = $"App {suffix}" });
        await _apps.AddRepositoryAsync(new Repository
        {
            Id = $"repo-{suffix}", AppId = appId, Name = "fake-ab1-contrato-api-pagamentos", Provider = FakeRepositoryContentSource.ProviderName,
            ExternalId = $"repo-{suffix}", Sigla = sigla, Role = RepositoryRoles.Contract, Status = "active"
        });
        return (appId, sigla);
    }

    private Task AnalyzeAsync(string sigla, int revision, DateTimeOffset at) =>
        new AnalysisService(
                new FakeRepositoryContentSource(revision), new ScannerRegistry([new OpenApiScanner()]),
                [new DependencyFactProcessor(), new IntegrationFactProcessor(new AnalysisOptions()), new ApiContractFactProcessor()],
                _apps, _analyses, new AnalysisOptions(), new ScanOptions { SiglaPattern = "^[a-z0-9]{3}$" }, new FixedTime(at))
            .AnalyzeAsync([sigla], dryRun: false);

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static StepInput Step(string appId, string method, string path, string? label = null) => new(appId, method, path, label, null);

    /// <summary>A jornada de Fundos: um sistema com uma jornada-folha cujos passos apontam para o contrato de demonstração.</summary>
    private async Task<(Journey System, Journey Purchase)> BuildFundsJourneyAsync(string appId)
    {
        var service = Service();
        var system = await service.CreateAsync(new JourneyInput($"Sistema {Guid.NewGuid():N}"[..20], null, "sistema", null, null, []));
        var purchase = await service.CreateAsync(new JourneyInput("Compra de Fundos", null, "jornada", null, system.Id, ["tipo-produto:fundos"]));
        await service.ReplaceStepsAsync(purchase.Id,
        [
            Step(appId, "GET", "/pagamentos", "Consultar fundos"),
            Step(appId, "POST", "/simulacoes", "Simular"),
            Step(appId, "GET", "/pagamentos/{id}", "Detalhar"),
            Step(appId, "GET", "/relatorios", "Relatório"),
            Step(appId, "DELETE", "/simulacoes/{id}", "Cancelar"),
            Step(appId, "PUT", "/nao-existe", "Fora do contrato")
        ]);
        return (system, purchase);
    }

    [Fact]
    public async Task BeforeAnyAnalysis_TheStepsAreAnAppWithoutContract_AndTheImpactIsUnknown()
    {
        var (appId, _) = await SeedContractAppAsync();
        var (_, purchase) = await BuildFundsJourneyAsync(appId);

        var detail = await Service().GetDetailAsync(purchase.Id);
        var impact = await Service().GetImpactAsync(purchase.Id);

        Assert.All(detail.Steps, s => Assert.Equal(StepResolutions.AppWithoutContract, s.Resolution));
        Assert.Equal(6, impact.StepsWithoutComparison);
        Assert.Equal(0, impact.Breaking);
    }

    [Fact]
    public async Task AfterTheFirstAnalysis_TheStepsAreResolved_ButThereIsStillNothingToCompare()
    {
        var (appId, sigla) = await SeedContractAppAsync();
        var (_, purchase) = await BuildFundsJourneyAsync(appId);
        await AnalyzeAsync(sigla, revision: 0, T0);

        var detail = await Service().GetDetailAsync(purchase.Id);
        var impact = await Service().GetImpactAsync(purchase.Id);

        Assert.Equal(StepResolutions.Cataloged, detail.Steps[0].Resolution);
        Assert.Equal("Lista os pagamentos", detail.Steps[0].Endpoint!.Summary);
        Assert.Equal(["oauth (oauth2)"], detail.Steps[0].Endpoint!.Security);
        Assert.Equal(StepResolutions.Cataloged, detail.Steps[2].Resolution);
        Assert.Equal(StepResolutions.OutsideContract, detail.Steps[4].Resolution);   // ainda não existe na revisão 0
        Assert.Equal(StepResolutions.OutsideContract, detail.Steps[5].Resolution);
        Assert.Equal(6, impact.StepsWithoutComparison);
    }

    [Fact]
    public async Task AfterANewCommit_TheImpactShowsWhatBrokeInWhichStep()
    {
        var (appId, sigla) = await SeedContractAppAsync();
        var (system, purchase) = await BuildFundsJourneyAsync(appId);
        await AnalyzeAsync(sigla, revision: 0, T0);
        await AnalyzeAsync(sigla, revision: 1, T0.AddDays(1));

        var impact = await Service().GetImpactAsync(purchase.Id);

        // POST /simulacoes: contaOrigem obrigatório, valor mudou de tipo, valorImposto saiu. GET /pagamentos: valor de enum removido.
        // GET /relatorios: o endpoint saiu do contrato.
        Assert.Equal(5, impact.Breaking);
        Assert.Equal(3, impact.BreakingSteps);
        Assert.Equal(0, impact.StepsWithoutComparison);

        var simulate = impact.Steps.Single(s => s.Step.Path == "/simulacoes");
        Assert.Equal(3, simulate.Changes.Count(c => c.Severity == ContractChangeSeverities.Breaking));
        Assert.Contains(simulate.Changes, c => c.Kind == ContractChangeKinds.BecameRequired && c.FieldPath == "contaOrigem");

        Assert.True(impact.Steps.Single(s => s.Step.Path == "/relatorios").EndpointRemoved);

        // O passo escreveu {id} e o contrato agora diz {pagamentoId}: o passo continua achando o endpoint, que ficou deprecated.
        var renamed = impact.Steps.Single(s => s.Step.Path == "/pagamentos/{id}");
        Assert.Contains(renamed.Changes, c => c.Kind == ContractChangeKinds.EndpointDeprecated);
        Assert.DoesNotContain(renamed.Changes, c => c.Kind == ContractChangeKinds.EndpointRemoved);

        // Endpoint novo no contrato: seguro, e o passo que o usa acha o endpoint.
        Assert.Contains(impact.Steps.Single(s => s.Step.Path == "/simulacoes/{id}").Changes, c => c.Kind == ContractChangeKinds.EndpointAdded);

        // Passo fora do contrato não tem mudança nenhuma, mas a comparação existe: não é "sem comparação".
        var outside = impact.Steps.Single(s => s.Step.Path == "/nao-existe");
        Assert.Empty(outside.Changes);
        Assert.Equal(ContractChangeStates.Compared, outside.Comparison);

        // O impacto do sistema é o da jornada dele.
        Assert.Equal(impact.Breaking, (await Service().GetImpactAsync(system.Id)).Breaking);

        var detail = await Service().GetDetailAsync(purchase.Id);
        Assert.Equal(StepResolutions.OutsideContract, detail.Steps[3].Resolution);    // /relatorios saiu
        Assert.Equal(StepResolutions.Cataloged, detail.Steps[2].Resolution);          // /pagamentos/{id} casa com {pagamentoId}
        Assert.Equal(StepResolutions.Cataloged, detail.Steps[4].Resolution);          // /simulacoes/{id} agora existe
    }

    [Fact]
    public async Task ReanalyzingTheSameCommit_DoesNotChangeTheImpact()
    {
        var (appId, sigla) = await SeedContractAppAsync();
        var (_, purchase) = await BuildFundsJourneyAsync(appId);
        await AnalyzeAsync(sigla, revision: 0, T0);
        await AnalyzeAsync(sigla, revision: 1, T0.AddDays(1));
        var before = await Service().GetImpactAsync(purchase.Id);

        await AnalyzeAsync(sigla, revision: 1, T0.AddDays(2));
        var after = await Service().GetImpactAsync(purchase.Id);

        Assert.Equal(before.Breaking, after.Breaking);
        Assert.Equal(before.Steps.Sum(s => s.Changes.Count), after.Steps.Sum(s => s.Changes.Count));
    }

    [Fact]
    public async Task TheAppsOfTheSystem_AreDerivedFromTheStepsOfItsJourneys()
    {
        var (appId, _) = await SeedContractAppAsync();
        var (system, _) = await BuildFundsJourneyAsync(appId);

        var apps = (await Service().GetDetailAsync(system.Id)).Apps;

        var app = Assert.Single(apps);
        Assert.Equal(appId, app.AppId);
        Assert.Equal(6, app.StepCount);
    }

    [Fact]
    public async Task RemovingAnAppUsedByAStep_Conflicts_WithTheRealRepositories()
    {
        var (appId, _) = await SeedContractAppAsync();
        await BuildFundsJourneyAsync(appId);
        var appService = new AppService(_apps, _squads, new QualitySnapshotRepository(fixture.ConnectionFactory), new CheckRepository(fixture.ConnectionFactory), _journeys);

        await Assert.ThrowsAsync<DomainConflictException>(() => appService.DeleteAsync(appId));
    }
}
