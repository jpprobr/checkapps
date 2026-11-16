using CheckApps.Application.Analysis;
using CheckApps.Application.Checks;
using CheckApps.Application.Discovery;
using CheckApps.Application.Journeys;
using CheckApps.Application.Persistence;
using CheckApps.Domain.Checks;
using CheckApps.Domain.Discovery;
using CheckApps.Infrastructure.Analysis;

namespace CheckApps.Infrastructure.Seeding;

/// <summary>
/// Monta a base de demonstração completa (spec 023) por cima da massa básica, **só com os serviços que já existem** e sem rede:
/// descoberta pela fonte <c>fake</c>, triagem, análise das duas revisões do contrato de demonstração, uma árvore de jornadas com
/// passos e uma checagem. Espera um banco com a massa básica do <see cref="DemoDataSeeder"/> e a configuração do modo demo
/// (fonte <c>fake</c> e convenção <c>fake-{sigla}-</c>).
/// </summary>
public sealed class DemoEnvironmentBuilder(
    ScanService scanService, TriageService triage, JourneyService journeys, CheckService checks, ScannerRegistry scanners,
    IEnumerable<IAnalysisFactProcessor> factProcessors, IAppRepository apps, IAnalysisRepository analyses,
    AnalysisOptions analysisOptions, ScanOptions scanOptions, TimeProvider timeProvider)
{
    /// <summary>As siglas que a fonte <c>fake</c> conhece.</summary>
    public static readonly string[] Siglas = ["ab1", "cd2"];

    private const string Squad = "sq-1";

    public async Task<DemoEnvironmentReport> BuildAsync(CancellationToken cancellationToken = default)
    {
        await scanService.ScanAsync(Siglas, dryRun: false, cancellationToken);

        // Triagem: o app de pagamentos e o BFF dele, e o repositório de contrato vinculado ao app de pagamentos.
        var payments = await triage.CreateAppAsync(
            await DiscoveredIdAsync("ab1", "app-pagamentos", cancellationToken), "Pagamentos", Squad,
            "API de pagamentos e simulações (demonstração).", RepositoryRoles.Application, cancellationToken);
        var bff = await triage.CreateAppAsync(
            await DiscoveredIdAsync("ab1", "app-bff-pagamentos", cancellationToken), "BFF Pagamentos", Squad,
            "BFF do canal de pagamentos (demonstração).", RepositoryRoles.Application, cancellationToken);
        await triage.LinkAsync(
            await DiscoveredIdAsync("ab1", "contrato-api-pagamentos", cancellationToken), payments.Id, RepositoryRoles.Contract, cancellationToken);

        // Análise das duas revisões do contrato, em momentos diferentes: é o que dá uma comparação para mostrar.
        var now = timeProvider.GetUtcNow();
        await AnalyzeAsync(revision: 0, now.AddDays(-1), cancellationToken);
        await AnalyzeAsync(revision: 1, now, cancellationToken);

        var journeyCount = await BuildJourneysAsync(payments.Id, cancellationToken);
        var checkTitle = await BuildCheckAsync(cancellationToken);

        return new DemoEnvironmentReport([payments.Name, bff.Name], journeyCount, checkTitle);
    }

    private async Task<string> DiscoveredIdAsync(string sigla, string suffix, CancellationToken cancellationToken)
    {
        var name = new RepositoryNameConvention(scanOptions.NameTemplate ?? "fake-{sigla}-").PrefixFor(sigla) + suffix;
        var items = await triage.ListAsync(null, sigla, CatalogScope.Any, cancellationToken);
        return items.FirstOrDefault(i => string.Equals(i.Repository.Name, name, StringComparison.OrdinalIgnoreCase))?.Repository.Id
            ?? throw new InvalidOperationException($"O repositório de demonstração '{name}' não foi descoberto.");
    }

    private Task AnalyzeAsync(int revision, DateTimeOffset at, CancellationToken cancellationToken) =>
        new AnalysisService(
                new FakeRepositoryContentSource(revision), scanners, factProcessors, apps, analyses, analysisOptions, scanOptions,
                new FixedTimeProvider(at))
            .AnalyzeAsync(Siglas, dryRun: false, force: true, cancellationToken);

    private async Task<int> BuildJourneysAsync(string appId, CancellationToken cancellationToken)
    {
        var platform = await journeys.CreateAsync(
            new JourneyInput("Plataforma de pagamentos", "Exemplo de plataforma (demonstração).", "plataforma", null, null, []), cancellationToken);
        var system = await journeys.CreateAsync(
            new JourneyInput("Portal de pagamentos", null, "sistema", Squad, platform.Id, ["tipo-produto:pagamentos"]), cancellationToken);
        var pay = await journeys.CreateAsync(
            new JourneyInput("Simular e pagar", "Consulta, simula e confirma um pagamento.", "jornada", Squad, system.Id, []), cancellationToken);
        var cancel = await journeys.CreateAsync(
            new JourneyInput("Cancelar simulação", null, "jornada", Squad, system.Id, []), cancellationToken);

        await journeys.ReplaceStepsAsync(pay.Id,
        [
            new StepInput(appId, "GET", "/pagamentos", "Consultar pagamentos", null),
            new StepInput(appId, "POST", "/simulacoes", "Simular", null),
            new StepInput(appId, "GET", "/pagamentos/{id}", "Detalhar o pagamento", null),
            new StepInput(appId, "GET", "/relatorios", "Emitir relatório", null)
        ], cancellationToken);
        await journeys.ReplaceStepsAsync(cancel.Id, [new StepInput(appId, "DELETE", "/simulacoes/{id}", "Cancelar", null)], cancellationToken);

        return 4;
    }

    private async Task<string> BuildCheckAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var check = await checks.CreateAsync(new CheckFields(
            "Atualizar para .NET 10", "Exemplo de checagem com status variados (demonstração).", "Atualização de versão", "Demonstração",
            today.AddDays(-14), today.AddDays(30)), cancellationToken);

        var appIds = (await apps.ListAllAsync(cancellationToken)).Select(a => a.Id).Order(StringComparer.Ordinal).ToList();
        await checks.AddAppsAsync(check.Id, appIds, cancellationToken);

        CheckItemStatus[] statuses = [CheckItemStatus.Conforme, CheckItemStatus.EmAndamento, CheckItemStatus.Pendente, CheckItemStatus.Concluido, CheckItemStatus.NaoSeAplica];
        for (var i = 0; i < appIds.Count; i++)
        {
            var status = statuses[i % statuses.Length];
            if (status != CheckItemStatus.Pendente)
            {
                await checks.UpdateItemAsync(check.Id, appIds[i], status, status == CheckItemStatus.NaoSeAplica ? "App sem .NET." : null, cancellationToken);
            }
        }

        return check.Title;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

/// <summary>O que a base de demonstração ganhou, para o seed imprimir.</summary>
public sealed record DemoEnvironmentReport(IReadOnlyList<string> CreatedApps, int Journeys, string Check);
