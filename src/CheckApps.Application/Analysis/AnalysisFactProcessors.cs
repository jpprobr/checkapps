using CheckApps.Domain.Analysis;

namespace CheckApps.Application.Analysis;

/// <summary>
/// Transforma as saídas brutas de um tipo em fatos que podem ser gravados (spec 021). É aqui que fica a regra de cada tipo,
/// inclusive a redação: o <see cref="AnalysisService"/> não conhece nenhum tipo de fato.
/// </summary>
public interface IAnalysisFactProcessor
{
    /// <summary>Nome do tipo no relatório da análise (ex.: <c>integrações</c>).</summary>
    string Label { get; }

    AnalysisFactBatch Process(ScannerResult result, AnalysisFactContext context);
}

/// <summary>De que análise e de que scanner vêm as saídas.</summary>
public sealed record AnalysisFactContext(string AnalysisId, string ScannerId, string ScannerVersion);

/// <summary>Os fatos prontos para gravar, com o que o processador tem a avisar. Nunca traz valor bruto.</summary>
public sealed record AnalysisFactBatch(
    IReadOnlyList<IAnalysisFact> Facts, IReadOnlyList<string>? Warnings = null, IReadOnlyList<string>? Notes = null);

/// <summary>Quantos fatos de um tipo uma análise produziu.</summary>
public sealed record AnalysisFactCount(string Label, int Count);

/// <summary>Pacotes e frameworks (spec 013): só ganham identidade e proveniência.</summary>
public sealed class DependencyFactProcessor : IAnalysisFactProcessor
{
    public const string FactLabel = "fatos";

    public string Label => FactLabel;

    public AnalysisFactBatch Process(ScannerResult result, AnalysisFactContext context) =>
        new(result.Outputs.OfType<ScannerFact>().Select(fact => new RepositoryDependency
        {
            Id = Guid.NewGuid().ToString("n"),
            AnalysisId = context.AnalysisId,
            Ecosystem = fact.Ecosystem,
            Kind = fact.Kind,
            Name = fact.Name,
            Version = fact.Version,
            ProjectPath = fact.ProjectPath,
            SourceFile = fact.SourceFile,
            Origin = FactOrigins.Parser,
            Confidence = fact.Confidence,
            ConfidenceNote = fact.ConfidenceNote,
            ScannerId = context.ScannerId,
            ScannerVersion = context.ScannerVersion
        }).ToList<IAnalysisFact>());
}

/// <summary>
/// Endpoints declarados em contratos (spec 021). O scanner já não devolve exemplos, valores padrão, descrições nem
/// <c>servers</c>; aqui o resumo, único texto livre gravado, é truncado.
/// </summary>
public sealed class ApiContractFactProcessor : IAnalysisFactProcessor
{
    public const string FactLabel = "endpoints";

    private const int MaxSummaryLength = 200;

    public string Label => FactLabel;

    public AnalysisFactBatch Process(ScannerResult result, AnalysisFactContext context) =>
        new(result.Outputs.OfType<ScannerApiEndpoint>().Select(endpoint => ToFact(endpoint, context)).ToList<IAnalysisFact>());

    private static RepositoryApiEndpoint ToFact(ScannerApiEndpoint endpoint, AnalysisFactContext context)
    {
        var id = Guid.NewGuid().ToString("n");
        return new RepositoryApiEndpoint
        {
            Id = id,
            AnalysisId = context.AnalysisId,
            Mechanism = endpoint.Mechanism,
            Method = endpoint.Method,
            Path = endpoint.Path,
            PathKey = ApiPath.KeyOf(endpoint.Path),
            DeclaredId = endpoint.DeclaredId,
            Summary = endpoint.Summary is { Length: > MaxSummaryLength } summary ? summary[..MaxSummaryLength] : endpoint.Summary,
            Tags = endpoint.Tags,
            Security = endpoint.Security,
            Deprecated = endpoint.Deprecated,
            ApiTitle = endpoint.ApiTitle,
            ApiVersion = endpoint.ApiVersion,
            SourceFile = endpoint.SourceFile,
            Origin = FactOrigins.Parser,
            Confidence = endpoint.Confidence,
            ConfidenceNote = endpoint.ConfidenceNote,
            ScannerId = context.ScannerId,
            ScannerVersion = context.ScannerVersion,
            Fields = endpoint.Fields.Select(field => new RepositoryApiField
            {
                Id = Guid.NewGuid().ToString("n"),
                EndpointId = id,
                Location = field.Location,
                StatusCode = field.StatusCode,
                MediaType = field.MediaType,
                Path = field.Path,
                Type = field.Type,
                Format = field.Format,
                Required = field.Required,
                EnumValues = field.EnumValues,
                Note = field.Note
            }).ToList()
        };
    }
}

/// <summary>Integrações declaradas (spec 014): o valor bruto do candidato morre aqui; só o endereço normalizado segue adiante.</summary>
public sealed class IntegrationFactProcessor(AnalysisOptions options) : IAnalysisFactProcessor
{
    public const string FactLabel = "integrações";

    public string Label => FactLabel;

    public AnalysisFactBatch Process(ScannerResult result, AnalysisFactContext context)
    {
        var normalized = IntegrationNormalizer.Normalize(
            result.Outputs.OfType<ScannerIntegration>().ToList(), context.AnalysisId, context.ScannerId, context.ScannerVersion,
            options.EffectiveEnvironmentAliases, () => Guid.NewGuid().ToString("n"));

        return new AnalysisFactBatch(normalized.Items.ToList<IAnalysisFact>(), normalized.Warnings, normalized.Notes);
    }
}
