namespace CheckApps.Api.Contracts;

// Spec 014. Estado, ambiente e confiança circulam como códigos em texto (resolvida | parametrizada | nao-resolvida;
// dev | hom | prod; alta | media | baixa). A API só lê: nenhum destes contratos dispara análise. Nenhum campo traz o
// valor bruto lido do arquivo, só o endereço já normalizado.

/// <param name="Host">Nulo quando a integração não foi resolvida; pode ser um modelo com <c>{env}</c>.</param>
/// <param name="Environment">Nome do ambiente como está no arquivo; nulo no arquivo base.</param>
/// <param name="EnvironmentKind">Categoria normalizada; nula se o nome do ambiente não é conhecido.</param>
public record IntegrationDto(
    string Id, string Mechanism, string? Scheme, string? Host, int? Port, string? BasePath, string Reference, string ConfigKey,
    string? Environment, string? EnvironmentKind, string State, string ProjectPath, string SourceFile, string Origin,
    string Confidence, string? ConfidenceNote, string ScannerId, string ScannerVersion);

public record IntegrationSectionDto(string Reference, IReadOnlyList<IntegrationDto> Integrations);

/// <param name="Analysis">A corrente; nula se o repositório nunca foi analisado.</param>
/// <param name="LastFailure">Tentativa que falhou depois da corrente.</param>
public record RepositoryIntegrationsDto(
    string RepositoryId, string RepositoryName, string? RepositoryStatus, string? Role, string? Url, string? Sigla,
    AnalysisDto? Analysis, AnalysisDto? LastFailure, IReadOnlyList<IntegrationSectionDto> Sections);

public record AppIntegrationsDto(string AppId, IReadOnlyList<RepositoryIntegrationsDto> Repositories);

public record SquadRefDto(string Id, string Name);

/// <param name="Key"><c>host[:porta]</c> literal, a identidade do host nesta spec.</param>
/// <param name="IsTemplate">O host tem <c>{env}</c>.</param>
public record HostSummaryDto(
    string Key, string Host, int? Port, bool IsTemplate, int AppCount, int RepositoryCount, IReadOnlyList<SquadRefDto> Squads,
    IReadOnlyList<string> References, IReadOnlyList<string> EnvironmentKinds, DateTimeOffset LatestAnalyzedAt);

/// <param name="Coverage">Repositórios catalogados nunca analisados: a lista pode estar incompleta.</param>
public record HostListDto(IReadOnlyList<HostSummaryDto> Hosts, AnalysisCoverageDto Coverage);

public record HostUsageDto(
    string AppId, string AppName, string? SquadId, string? SquadName, string RepositoryId, string RepositoryName,
    string? RepositoryStatus, IntegrationDto Integration, DateTimeOffset AnalyzedAt);

public record HostDetailDto(string Key, string Host, int? Port, bool IsTemplate, IReadOnlyList<HostUsageDto> Usages);
