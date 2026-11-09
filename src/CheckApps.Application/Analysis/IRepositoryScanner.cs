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

/// <summary>
/// Algo que um scanner viu, ainda bruto e sem identidade. Cada tipo tem um <see cref="IAnalysisFactProcessor"/> que decide
/// o que é gravado (spec 021): um tipo novo não muda o <see cref="ScannerResult"/> nem o <see cref="AnalysisService"/>.
/// </summary>
public interface IScannerOutput;

/// <summary>Um fato visto por um scanner, ainda sem identidade nem análise.</summary>
public sealed record ScannerFact(
    string Ecosystem, string Kind, string Name, string? Version, string ProjectPath, string SourceFile,
    string Confidence, string? ConfidenceNote = null) : IScannerOutput;

/// <summary>
/// Um candidato a integração visto por um scanner (spec 014), **ainda bruto**: o valor pode trazer usuário, senha ou
/// query. Quem decide o que é gravado é o núcleo (<see cref="IntegrationNormalizer"/>); o valor bruto nunca é
/// persistido, logado nem posto em aviso.
/// </summary>
/// <param name="Reference">Seção de configuração (ex.: <c>IPagamentosServiceCliente</c>).</param>
/// <param name="ConfigKey">Chave completa (ex.: <c>IPagamentosServiceCliente:BaseUrl</c>).</param>
/// <param name="EnvironmentName">Nome do ambiente do arquivo (<c>Homolog</c>); nulo no arquivo base.</param>
/// <param name="RawTarget">O valor como está no arquivo.</param>
public sealed record ScannerIntegration(
    string Mechanism, string ProjectPath, string SourceFile, string Reference, string ConfigKey,
    string? EnvironmentName, string RawTarget) : IScannerOutput;

/// <summary>Um endpoint declarado num contrato (spec 021), visto por um scanner. Só estrutura: nunca exemplo, padrão, descrição ou <c>servers</c>.</summary>
/// <param name="Security">Esquemas efetivos, só nome e tipo.</param>
public sealed record ScannerApiEndpoint(
    string Mechanism, string Method, string Path, string? DeclaredId, string? Summary, IReadOnlyList<string> Tags,
    IReadOnlyList<string> Security, bool Deprecated, string? ApiTitle, string? ApiVersion, string SourceFile,
    string Confidence, string? ConfidenceNote, IReadOnlyList<ScannerApiField> Fields) : IScannerOutput;

/// <summary>Um campo achatado de um endpoint declarado (<c>cliente.contas[].numero</c>).</summary>
public sealed record ScannerApiField(
    string Location, string? StatusCode, string? MediaType, string Path, string? Type, string? Format, bool Required,
    IReadOnlyList<string> EnumValues, string? Note = null);

/// <summary>
/// As saídas e os avisos de uma execução. Aviso nunca traz conteúdo de arquivo: só o caminho e o que aconteceu.
/// <see cref="Notes"/> são informativas (ex.: quantos valores foram ignorados) e **não** tornam a análise parcial; um
/// aviso torna.
/// </summary>
public sealed record ScannerResult
{
    /// <param name="outputs">Saídas de outros tipos, além de pacotes e integrações.</param>
    public ScannerResult(
        IReadOnlyList<ScannerFact> facts, IReadOnlyList<string> warnings,
        IReadOnlyList<ScannerIntegration>? integrations = null, IReadOnlyList<string>? notes = null,
        IReadOnlyList<IScannerOutput>? outputs = null)
    {
        Facts = facts;
        Warnings = warnings;
        Integrations = integrations ?? [];
        Notes = notes ?? [];
        Outputs = [.. Facts, .. Integrations, .. outputs ?? []];
    }

    public IReadOnlyList<ScannerFact> Facts { get; }

    public IReadOnlyList<string> Warnings { get; }

    public IReadOnlyList<ScannerIntegration> Integrations { get; }

    public IReadOnlyList<string> Notes { get; }

    /// <summary>Tudo o que o scanner viu, de todos os tipos. É o que os processadores leem.</summary>
    public IReadOnlyList<IScannerOutput> Outputs { get; }
}

/// <summary>Os scanners conhecidos. Um repositório pode ter mais de um se aplicando.</summary>
public sealed class ScannerRegistry(IEnumerable<IRepositoryScanner> scanners)
{
    public IReadOnlyList<IRepositoryScanner> All { get; } = scanners.ToList();

    public IReadOnlyList<IRepositoryScanner> Applicable(IReadOnlyList<string> paths) =>
        All.Where(s => s.AppliesTo(paths)).ToList();
}
