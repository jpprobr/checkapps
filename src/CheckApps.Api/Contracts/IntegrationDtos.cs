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

/// <param name="Destinations">Por id da integração: o app destino, só quando o host tem vínculo <c>confirmado</c> (spec 017).</param>
/// <param name="Externals">Por id da integração: a marca de externo (host de serviço fora do catálogo, sem app), spec 019.</param>
public record IntegrationSectionDto(
    string Reference, IReadOnlyList<IntegrationDto> Integrations, IReadOnlyDictionary<string, HostLinkMatchDto> Destinations,
    IReadOnlyDictionary<string, HostExternalMatchDto> Externals);

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
    IReadOnlyList<string> References, IReadOnlyList<string> EnvironmentKinds, DateTimeOffset LatestAnalyzedAt, HostLinkInfoDto Link);

/// <param name="Coverage">Repositórios catalogados nunca analisados: a lista pode estar incompleta.</param>
public record HostListDto(IReadOnlyList<HostSummaryDto> Hosts, AnalysisCoverageDto Coverage);

public record HostUsageDto(
    string AppId, string AppName, string? SquadId, string? SquadName, string RepositoryId, string RepositoryName,
    string? RepositoryStatus, IntegrationDto Integration, DateTimeOffset AnalyzedAt);

public record HostDetailDto(
    string Key, string Host, int? Port, bool IsTemplate, IReadOnlyList<HostUsageDto> Usages, HostLinkInfoDto Link);

// Spec 017: vínculo host→app, sempre decisão de pessoa. State = confirmado | rejeitado; Via = exact | template.

/// <param name="Template">A chave do modelo (<c>empresa.{env}.exemplo.com</c>) quando <c>Via</c> é <c>template</c>.</param>
public record HostLinkMatchDto(
    string LinkId, string HostKey, string AppId, string AppName, string State, string Via, string? Template,
    DateTimeOffset DecidedAt, string? Note);

/// <param name="Status"><c>confirmado</c>, <c>externo</c>, <c>rejeitado</c> (só há rejeitos) ou <c>sem-vinculo</c>.</param>
/// <param name="Confirmed">O app confirmado para o host (o exato vence o modelo); nulo se não há.</param>
/// <param name="External">A marca de "serviço externo ao catálogo" que vale para o host (spec 019); nula se não há.</param>
public record HostLinkInfoDto(
    string Status, HostLinkMatchDto? Confirmed, IReadOnlyList<HostLinkMatchDto> Rejected, HostExternalMatchDto? External);

/// <param name="Name">Nome livre do serviço externo; opcional.</param>
/// <param name="Template">A chave do modelo quando <c>Via</c> é <c>template</c>.</param>
public record HostExternalMatchDto(
    string ExternalId, string HostKey, string? Name, string Via, string? Template, DateTimeOffset DecidedAt, string? Note);

/// <param name="Seen"><c>false</c> = nenhuma integração das análises correntes casa com o vínculo ("não visto na última análise").</param>
public record HostLinkDto(
    string Id, string HostKey, bool IsTemplate, string AppId, string AppName, string State, string Origin,
    DateTimeOffset DecidedAt, string? Note, bool Seen);

public record SetHostLinkRequest(string? AppId, string? State, string? Note);

public record SetHostExternalRequest(string? Name, string? Note);

/// <param name="State"><c>confirmado</c>, <c>rejeitado</c> ou <c>externo</c>; <c>AppId</c> é obrigatório salvo em <c>externo</c>.</param>
public record BatchHostLinkRequest(IReadOnlyList<string>? HostKeys, string? State, string? AppId, string? Name, string? Note);

/// <param name="Error">O texto do erro quando <c>Ok</c> é <c>false</c> (um host do lote pode falhar sem desfazer os outros).</param>
public record BatchHostResultDto(string HostKey, bool Ok, string? Error);
