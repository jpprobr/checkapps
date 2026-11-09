namespace CheckApps.Api.Contracts;

// Spec 021. Só a estrutura do contrato: nunca exemplos, valores padrão, descrições, servers nem extensões. Lugar, severidade e
// situação circulam como códigos em texto. A API só lê: nenhum destes contratos dispara análise.

/// <param name="Location"><c>path</c> | <c>query</c> | <c>header</c> | <c>cookie</c> | <c>pedido</c> | <c>resposta</c>.</param>
/// <param name="StatusCode">Status HTTP da resposta; nulo nos demais lugares.</param>
public record ApiFieldDto(
    string Location, string? StatusCode, string? MediaType, string Path, string? Type, string? Format, bool Required,
    IReadOnlyList<string> EnumValues, string? Note);

/// <param name="Mechanism">De onde veio: hoje só <c>openapi</c>, um contrato declarado, que pode estar desatualizado.</param>
public record ApiEndpointDto(
    string Id, string Mechanism, string Method, string Path, string? DeclaredId, string? Summary, IReadOnlyList<string> Tags,
    IReadOnlyList<string> Security, bool Deprecated, string? ApiTitle, string? ApiVersion, string SourceFile, string Origin,
    string Confidence, string? ConfidenceNote, string ScannerId, string ScannerVersion, IReadOnlyList<ApiFieldDto> Fields);

/// <param name="DeclaredIn"><c>repositorio-de-contrato</c> | <c>repositorio-da-aplicacao</c>, derivado do papel do repositório.</param>
/// <param name="Analysis">A corrente; nula se o repositório nunca foi analisado.</param>
public record RepositoryContractsDto(
    string RepositoryId, string RepositoryName, string? RepositoryStatus, string? Role, string? Url, string DeclaredIn,
    AnalysisDto? Analysis, AnalysisDto? LastFailure, IReadOnlyList<ApiEndpointDto> Endpoints);

public record AppContractsDto(string AppId, IReadOnlyList<RepositoryContractsDto> Repositories);

/// <param name="Severity"><c>quebra</c> | <c>atencao</c> | <c>segura</c>.</param>
/// <param name="FieldPath">Nulo quando a mudança é no endpoint inteiro.</param>
public record ContractChangeDto(
    string Severity, string Kind, string Method, string Path, string? Location, string? StatusCode, string? MediaType,
    string? FieldPath, string Description);

/// <param name="State"><c>comparado</c> | <c>primeira-analise</c> | <c>sem-contrato</c>.</param>
/// <param name="Analysis">A análise corrente.</param>
/// <param name="Baseline">A análise anterior comparada; nula se não há base.</param>
public record RepositoryContractChangesDto(
    string RepositoryId, string RepositoryName, string State, AnalysisDto? Analysis, AnalysisDto? Baseline,
    IReadOnlyList<ContractChangeDto> Changes, IReadOnlyList<string> Warnings);

public record AppContractChangesDto(string AppId, IReadOnlyList<RepositoryContractChangesDto> Repositories);
