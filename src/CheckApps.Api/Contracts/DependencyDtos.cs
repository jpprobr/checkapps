namespace CheckApps.Api.Contracts;

// Spec 013. Situação da análise e confiança circulam como códigos em texto (ok | parcial | sem-scanner | falha;
// alta | media | baixa), nunca como nomes de enum. A API só lê: nenhum destes contratos dispara análise.

/// <param name="IsInternal">Calculado na leitura a partir de <c>Analysis:InternalPackagePrefixes</c>; nunca gravado.</param>
public record DependencyFactDto(
    string Id, string Ecosystem, string Kind, string Name, string? Version, string ProjectPath, string SourceFile,
    string Origin, string Confidence, string? ConfidenceNote, string ScannerId, string ScannerVersion, bool IsInternal);

public record AnalysisDto(string Id, DateTimeOffset AnalyzedAt, string? CommitSha, string Status, IReadOnlyList<string> Warnings);

/// <param name="Analysis">A corrente; nula se o repositório nunca foi analisado.</param>
/// <param name="LastFailure">Tentativa que falhou depois da corrente.</param>
public record RepositoryDependenciesDto(
    string RepositoryId, string RepositoryName, string? RepositoryStatus, string? Role, string? Url, string? Sigla,
    AnalysisDto? Analysis, AnalysisDto? LastFailure,
    IReadOnlyList<DependencyFactDto> Frameworks, IReadOnlyList<DependencyFactDto> Packages);

public record AppDependenciesDto(
    string AppId, bool InternalPrefixesConfigured, IReadOnlyList<RepositoryDependenciesDto> Repositories);

public record PackageUsageDto(
    string AppId, string AppName, string? SquadId, string? SquadName, string RepositoryId, string RepositoryName,
    string? RepositoryStatus, string Name, string? Version, string ProjectPath, string Confidence, string? ConfidenceNote,
    bool IsInternal, DateTimeOffset AnalyzedAt);

public record PackageVersionSummaryDto(string Name, string? Version, int AppCount, bool IsInternal);

/// <param name="UnanalyzedRepositories">Repositórios catalogados que nunca foram analisados: a resposta pode estar incompleta.</param>
public record AnalysisCoverageDto(int UnanalyzedRepositories, DateTimeOffset? LatestAnalyzedAt);

public record PackageSearchResultDto(
    IReadOnlyList<PackageUsageDto> Usages, IReadOnlyList<PackageVersionSummaryDto> Versions, AnalysisCoverageDto Coverage,
    bool InternalPrefixesConfigured);

public record FrameworkAppDto(string AppId, string AppName, IReadOnlyList<string> Repositories);

/// <param name="Framework">Nulo = "sem informação".</param>
public record FrameworkCellDto(string? Framework, string? SquadId, string? SquadName, IReadOnlyList<FrameworkAppDto> Apps);

public record FrameworkOverviewDto(IReadOnlyList<FrameworkCellDto> Cells, AnalysisCoverageDto Coverage);
