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
/// com <c>force</c>); o volume lido é limitado; e **nenhum conteúdo de arquivo é guardado** — só os fatos e os
/// avisos. As saídas chegam brutas do scanner e passam pelo <see cref="IAnalysisFactProcessor"/> do tipo delas antes de
/// qualquer gravação; este serviço não conhece nenhum tipo de fato (spec 021).
/// </summary>
public class AnalysisService(
    IRepositoryContentSource source, ScannerRegistry scanners, IEnumerable<IAnalysisFactProcessor> factProcessors,
    IAppRepository apps, IAnalysisRepository analyses, AnalysisOptions options, ScanOptions scanOptions,
    TimeProvider timeProvider)
{
    private readonly IReadOnlyList<IAnalysisFactProcessor> _processors = factProcessors.ToList();

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
            return new AnalysisItem(catalog, AnalysisOutcome.Skipped, null, ["esta fonte não lê repositórios desse provedor"]);
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
            return new AnalysisItem(catalog, AnalysisOutcome.Unchanged, current.Status, []);
        }

        var warnings = new List<string>();
        var notes = new List<string>();
        if (tree.Truncated)
        {
            warnings.Add("a árvore de arquivos veio truncada pelo provedor; parte do repositório não foi vista.");
        }

        var paths = tree.Files.Select(f => f.Path).ToList();
        var applicable = scanners.Applicable(paths);
        var factsByProcessor = _processors.ToDictionary(p => p, _ => new List<IAnalysisFact>());

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
                await RunScannerAsync(scanner, repository, tree, analysis.Id, factsByProcessor, warnings, notes, cancellationToken);
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
            await analyses.SaveAsync(analysis, factsByProcessor.Values.SelectMany(f => f).ToList(), cancellationToken);
        }

        var counts = _processors.Select(p => new AnalysisFactCount(p.Label, factsByProcessor[p].Count)).ToList();
        return new AnalysisItem(catalog, AnalysisOutcome.Analyzed, status, stored, counts);
    }

    /// <summary>Prefixo das notas informativas dentro da lista de avisos da análise.</summary>
    public const string NotePrefix = "nota: ";

    private async Task RunScannerAsync(
        IRepositoryScanner scanner, Repository repository, RepositoryTree tree, string analysisId,
        Dictionary<IAnalysisFactProcessor, List<IAnalysisFact>> factsByProcessor,
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
            return;
        }

        warnings.AddRange(result.Warnings);
        notes.AddRange(result.Notes);

        // Redação no núcleo (specs 014 e 021): nada do que o scanner devolveu é gravado sem passar pelo processador do tipo.
        var context = new AnalysisFactContext(analysisId, scanner.Id, scanner.Version);
        foreach (var processor in _processors)
        {
            var batch = processor.Process(result, context);
            factsByProcessor[processor].AddRange(batch.Facts);
            warnings.AddRange(batch.Warnings ?? []);
            notes.AddRange(batch.Notes ?? []);
        }
    }

    /// <summary>A falha fica no histórico (a tela mostra "última tentativa falhou"), mas não vira a análise corrente.</summary>
    private async Task<AnalysisItem> RecordFailureAsync(
        CatalogRepository catalog, string message, bool dryRun, CancellationToken cancellationToken)
    {
        if (!dryRun)
        {
            await analyses.SaveAsync(new RepositoryAnalysis
            {
                Id = Guid.NewGuid().ToString("n"),
                RepositoryId = catalog.Repository.Id,
                AnalyzedAt = timeProvider.GetUtcNow(),
                Status = AnalysisStatuses.Failed,
                Warnings = [message]
            }, [], cancellationToken);
        }

        return new AnalysisItem(catalog, AnalysisOutcome.Failed, AnalysisStatuses.Failed, [message]);
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
/// <param name="FactCounts">Quantos fatos de cada tipo a análise produziu; vazio se não houve análise nova.</param>
public record AnalysisItem(
    CatalogRepository Catalog, AnalysisOutcome Outcome, string? Status, IReadOnlyList<string> Warnings,
    IReadOnlyList<AnalysisFactCount>? FactCounts = null)
{
    public int FactCount => CountOf(DependencyFactProcessor.FactLabel);

    public int IntegrationCount => CountOf(IntegrationFactProcessor.FactLabel);

    private int CountOf(string label) => FactCounts?.FirstOrDefault(c => c.Label == label)?.Count ?? 0;
}

public record AnalysisReport(bool DryRun, IReadOnlyList<AnalysisItem> Items)
{
    public int Count(AnalysisOutcome outcome) => Items.Count(i => i.Outcome == outcome);

    /// <summary>Quantas análises (novas) terminaram com a situação informada.</summary>
    public int CountStatus(string status) => Items.Count(i => i.Outcome == AnalysisOutcome.Analyzed && i.Status == status);
}
