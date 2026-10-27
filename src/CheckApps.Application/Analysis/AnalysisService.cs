using CheckApps.Application.Discovery;
using CheckApps.Application.Persistence;
using CheckApps.Domain.Analysis;
using CheckApps.Domain.Discovery;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.Application.Analysis;

/// <summary>
/// Lê os arquivos dos repositórios **já catalogados** de uma ou mais siglas e grava os fatos que os scanners extraem
/// (spec 013, ADR-011). Não conhece nenhuma tecnologia: lista os arquivos pela <see cref="IRepositoryContentSource"/>,
/// escolhe os scanners pela lista de caminhos, lê só o que eles pedem e grava uma análise por repositório.
///
/// Regras: a falha de um repositório não interrompe os outros; um commit já analisado não gera nada de novo (salvo
/// com <c>force</c>); o volume lido é limitado; e **nenhum conteúdo de arquivo é guardado** — só os fatos, as
/// integrações já normalizadas (spec 014) e os avisos. Os candidatos a integração chegam brutos do scanner e passam
/// pelo <see cref="IntegrationNormalizer"/> antes de qualquer gravação.
/// </summary>
public class AnalysisService(
    IRepositoryContentSource source, ScannerRegistry scanners, IAppRepository apps, IAnalysisRepository analyses,
    AnalysisOptions options, ScanOptions scanOptions, TimeProvider timeProvider)
{
    /// <summary>O que falta para a fonte funcionar, ou <c>null</c>. Sem valores de configuração.</summary>
    public string? ConfigurationProblem => source.ConfigurationProblem;

    /// <exception cref="ArgumentException">Nenhuma sigla, ou fora do formato — antes de consultar a fonte.</exception>
    /// <exception cref="InvalidOperationException">A fonte não está configurada (ex.: sem credencial).</exception>
    /// <param name="force">Reanalisa mesmo quando o commit é o da análise corrente (grava uma análise nova).</param>
    public async Task<AnalysisReport> AnalyzeAsync(
        IEnumerable<string>? siglas, bool dryRun, bool force = false, CancellationToken cancellationToken = default)
    {
        var scope = ScanScope.Create(siglas, scanOptions.SiglaPattern);

        if (ConfigurationProblem is { } problem)
        {
            throw new InvalidOperationException(problem);
        }

        var items = new List<AnalysisItem>();
        foreach (var sigla in scope.Siglas)
        {
            foreach (var catalog in await apps.ListRepositoriesBySiglaAsync(sigla, cancellationToken))
            {
                items.Add(await AnalyzeRepositoryAsync(catalog, dryRun, force, cancellationToken));
            }
        }

        return new AnalysisReport(dryRun, items);
    }

    private async Task<AnalysisItem> AnalyzeRepositoryAsync(
        CatalogRepository catalog, bool dryRun, bool force, CancellationToken cancellationToken)
    {
        var repository = catalog.Repository;
        if (!source.CanRead(repository))
        {
            return new AnalysisItem(catalog, AnalysisOutcome.Skipped, null, 0, ["esta fonte não lê repositórios desse provedor"]);
        }

        RepositoryTree tree;
        try
        {
            tree = await source.ListAsync(repository, cancellationToken);
        }
        catch (RepositorySourceException ex)
        {
            return await RecordFailureAsync(catalog, ex.Message, dryRun, cancellationToken);
        }

        var current = await analyses.GetCurrentAsync(repository.Id, cancellationToken);
        if (!force && current is not null && current.CommitSha == tree.CommitSha)
        {
            return new AnalysisItem(catalog, AnalysisOutcome.Unchanged, current.Status, 0, []);
        }

        var warnings = new List<string>();
        var notes = new List<string>();
        if (tree.Truncated)
        {
            warnings.Add("a árvore de arquivos veio truncada pelo provedor; parte do repositório não foi vista.");
        }

        var paths = tree.Files.Select(f => f.Path).ToList();
        var applicable = scanners.Applicable(paths);
        var facts = new List<RepositoryDependency>();
        var integrations = new List<RepositoryIntegration>();

        var analysis = new RepositoryAnalysis
        {
            Id = Guid.NewGuid().ToString("n"),
            RepositoryId = repository.Id,
            AnalyzedAt = timeProvider.GetUtcNow(),
            CommitSha = tree.CommitSha,
            Status = AnalysisStatuses.Ok   // ajustado abaixo
        };

        try
        {
            foreach (var scanner in applicable)
            {
                var run = await RunScannerAsync(scanner, repository, tree, analysis.Id, warnings, notes, cancellationToken);
                facts.AddRange(run.Facts);
                integrations.AddRange(run.Integrations);
            }
        }
        catch (RepositorySourceException ex)
        {
            return await RecordFailureAsync(catalog, ex.Message, dryRun, cancellationToken);
        }

        var status = warnings.Count > 0
            ? AnalysisStatuses.Partial
            : applicable.Count == 0 ? AnalysisStatuses.NoScanner : AnalysisStatuses.Ok;

        // As notas são informativas: ficam junto dos avisos (prefixo "nota:"), mas não decidem a situação acima.
        var stored = warnings.Concat(notes.Select(n => $"{NotePrefix}{n}")).ToList();
        analysis = analysis with { Status = status, Warnings = stored };

        if (!dryRun)
        {
            await analyses.AddAsync(analysis, facts, integrations, cancellationToken);
        }

        return new AnalysisItem(catalog, AnalysisOutcome.Analyzed, status, facts.Count, stored, integrations.Count);
    }

    /// <summary>Prefixo das notas informativas dentro da lista de avisos da análise.</summary>
    public const string NotePrefix = "nota: ";

    private sealed record ScannerRun(IReadOnlyList<RepositoryDependency> Facts, IReadOnlyList<RepositoryIntegration> Integrations);

    private async Task<ScannerRun> RunScannerAsync(
        IRepositoryScanner scanner, Repository repository, RepositoryTree tree, string analysisId,
        List<string> warnings, List<string> notes, CancellationToken cancellationToken)
    {
        var sizes = tree.Files.ToDictionary(f => f.Path, f => f.Size, StringComparer.OrdinalIgnoreCase);
        var selected = scanner.SelectFiles(tree.Files.Select(f => f.Path).ToList());

        if (selected.Count > options.MaxProjectFiles)
        {
            warnings.Add($"o repositório tem {selected.Count} arquivos de interesse para o scanner {scanner.Id}; só os primeiros {options.MaxProjectFiles} foram lidos.");
            selected = selected.Take(options.MaxProjectFiles).ToList();
        }

        var contents = new Dictionary<string, string>();
        foreach (var path in selected)
        {
            if (sizes.TryGetValue(path, out var size) && size > options.MaxFileBytes)
            {
                warnings.Add($"{path}: maior que o limite de leitura ({options.MaxFileBytes} bytes); ignorado.");
                continue;
            }

            var text = await source.ReadFileAsync(repository, tree.CommitSha, path, cancellationToken);
            if (text is null)
            {
                warnings.Add($"{path}: não foi encontrado no commit analisado; ignorado.");
                continue;
            }

            contents[path] = text;
        }

        ScannerResult result;
        try
        {
            result = scanner.Analyze(contents);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Um scanner com defeito não derruba a execução; só o tipo da exceção, nunca o conteúdo lido.
            warnings.Add($"o scanner {scanner.Id} falhou ({ex.GetType().Name}); os fatos dele foram descartados.");
            return new ScannerRun([], []);
        }

        warnings.AddRange(result.Warnings);
        notes.AddRange(result.Notes);

        // Redação no núcleo (spec 014): o valor bruto do candidato morre aqui; só o endereço normalizado segue adiante.
        var normalized = IntegrationNormalizer.Normalize(
            result.Integrations, analysisId, scanner.Id, scanner.Version, options.EffectiveEnvironmentAliases,
            () => Guid.NewGuid().ToString("n"));
        warnings.AddRange(normalized.Warnings);
        notes.AddRange(normalized.Notes);

        var facts = result.Facts.Select(fact => new RepositoryDependency
        {
            Id = Guid.NewGuid().ToString("n"),
            AnalysisId = analysisId,
            Ecosystem = fact.Ecosystem,
            Kind = fact.Kind,
            Name = fact.Name,
            Version = fact.Version,
            ProjectPath = fact.ProjectPath,
            SourceFile = fact.SourceFile,
            Origin = FactOrigins.Parser,
            Confidence = fact.Confidence,
            ConfidenceNote = fact.ConfidenceNote,
            ScannerId = scanner.Id,
            ScannerVersion = scanner.Version
        }).ToList();

        return new ScannerRun(facts, normalized.Items);
    }

    /// <summary>A falha fica no histórico (a tela mostra "última tentativa falhou"), mas não vira a análise corrente.</summary>
    private async Task<AnalysisItem> RecordFailureAsync(
        CatalogRepository catalog, string message, bool dryRun, CancellationToken cancellationToken)
    {
        if (!dryRun)
        {
            await analyses.AddAsync(new RepositoryAnalysis
            {
                Id = Guid.NewGuid().ToString("n"),
                RepositoryId = catalog.Repository.Id,
                AnalyzedAt = timeProvider.GetUtcNow(),
                Status = AnalysisStatuses.Failed,
                Warnings = [message]
            }, [], null, cancellationToken);
        }

        return new AnalysisItem(catalog, AnalysisOutcome.Failed, AnalysisStatuses.Failed, 0, [message]);
    }
}

public enum AnalysisOutcome
{
    /// <summary>Uma análise nova (ou, na simulação, que seria gravada).</summary>
    Analyzed,

    /// <summary>O commit é o mesmo da análise corrente: nada foi gravado.</summary>
    Unchanged,

    /// <summary>Não foi possível ler o repositório.</summary>
    Failed,

    /// <summary>A fonte configurada não lê o provedor deste repositório.</summary>
    Skipped
}

/// <param name="Status">A situação da análise (<see cref="AnalysisStatuses"/>); nula se foi pulado.</param>
/// <param name="Warnings">Avisos e notas (estas com o prefixo <see cref="AnalysisService.NotePrefix"/>); nunca trazem valor de arquivo.</param>
public record AnalysisItem(
    CatalogRepository Catalog, AnalysisOutcome Outcome, string? Status, int FactCount, IReadOnlyList<string> Warnings,
    int IntegrationCount = 0);

public record AnalysisReport(bool DryRun, IReadOnlyList<AnalysisItem> Items)
{
    public int Count(AnalysisOutcome outcome) => Items.Count(i => i.Outcome == outcome);

    /// <summary>Quantas análises (novas) terminaram com a situação informada.</summary>
    public int CountStatus(string status) => Items.Count(i => i.Outcome == AnalysisOutcome.Analyzed && i.Status == status);
}
