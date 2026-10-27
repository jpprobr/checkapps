namespace CheckApps.Application.Analysis;

/// <summary>
/// Scanner de uma tecnologia (ADR-011): lê arquivos de um repositório e devolve fatos. É um parser — mesmo conteúdo,
/// mesmo resultado, sem rede e sem relógio — e é o único lugar que conhece o formato dos arquivos da sua tecnologia.
/// Quem lê os arquivos e grava a análise é o <see cref="AnalysisService"/>.
/// </summary>
public interface IRepositoryScanner
{
    /// <summary>Identificador estável, gravado em cada fato (ex.: <c>dotnet</c>).</summary>
    string Id { get; }

    /// <summary>Versão do scanner, gravada em cada fato: dá para saber se uma diferença veio do código ou do scanner.</summary>
    string Version { get; }

    /// <summary>Este scanner se aplica ao repositório, a partir da lista de caminhos? (ex.: existe algum <c>*.csproj</c>)</summary>
    bool AppliesTo(IReadOnlyList<string> paths);

    /// <summary>Quais caminhos o scanner precisa ler. O serviço só lê estes, respeitando os limites configurados.</summary>
    IReadOnlyList<string> SelectFiles(IReadOnlyList<string> paths);

    /// <param name="files">Caminho → conteúdo dos arquivos selecionados que foi possível ler.</param>
    ScannerResult Analyze(IReadOnlyDictionary<string, string> files);
}

/// <summary>Um fato visto por um scanner, ainda sem identidade nem análise.</summary>
public sealed record ScannerFact(
    string Ecosystem, string Kind, string Name, string? Version, string ProjectPath, string SourceFile,
    string Confidence, string? ConfidenceNote = null);

/// <summary>
/// Os fatos e os avisos de uma execução. Aviso nunca traz conteúdo de arquivo: só o caminho e o que aconteceu.
/// </summary>
public sealed record ScannerResult(IReadOnlyList<ScannerFact> Facts, IReadOnlyList<string> Warnings);

/// <summary>Os scanners conhecidos. Um repositório pode ter mais de um se aplicando.</summary>
public sealed class ScannerRegistry(IEnumerable<IRepositoryScanner> scanners)
{
    public IReadOnlyList<IRepositoryScanner> All { get; } = scanners.ToList();

    public IReadOnlyList<IRepositoryScanner> Applicable(IReadOnlyList<string> paths) =>
        All.Where(s => s.AppliesTo(paths)).ToList();
}
