namespace CheckApps.Api.Contracts;

// Spec 022. Nível, resolução e comparação circulam como códigos em texto. A resolução dos passos e o impacto são calculados na leitura, a
// cada chamada, e nunca gravados. A API só lê os contratos: nenhum destes endpoints dispara análise nem exige credencial do provedor.

/// <param name="Level">Texto livre (<c>plataforma</c>, <c>sistema</c>, <c>jornada</c>…).</param>
/// <param name="Tags">Classificação para pessoas; a convenção <c>chave:valor</c> é só orientação.</param>
/// <param name="Source">Origem do cadastro: hoje só <c>MANUAL</c>.</param>
public record JourneyDto(
    string Id, string Name, string? Description, string? Level, string? SquadId, string? ParentId, IReadOnlyList<string> Tags,
    string Source, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record JourneySummaryDto(
    string Id, string Name, string? Level, string? SquadId, string? SquadName, string? ParentId, IReadOnlyList<string> Tags,
    int StepCount, int ChildCount);

/// <param name="Summary">Resumo do endpoint no contrato, ou nulo se o passo não o achou.</param>
public record ResolvedEndpointDto(
    string? Summary, IReadOnlyList<string> Security, bool Deprecated, string Confidence, string RepositoryName, string SourceFile);

/// <param name="Resolution"><c>catalogado</c> | <c>fora-do-contrato</c> | <c>app-sem-contrato</c>.</param>
/// <param name="Endpoint">O endpoint do contrato corrente que o passo encontrou; nulo se não achou.</param>
public record JourneyStepDto(
    string Id, int Position, string AppId, string AppName, string Method, string Path, string? Label, string? Note, string Resolution,
    ResolvedEndpointDto? Endpoint);

public record JourneyAppDto(string AppId, string AppName, int StepCount);

public record JourneyPathItemDto(string Id, string Name, string? Level);

/// <param name="Path">Os ancestrais, da raiz até o pai.</param>
/// <param name="Apps">Derivados dos passos da subárvore; não são digitados.</param>
public record JourneyDetailDto(
    JourneyDto Journey, string? SquadName, IReadOnlyList<JourneyPathItemDto> Path, IReadOnlyList<JourneySummaryDto> Children,
    IReadOnlyList<JourneyStepDto> Steps, IReadOnlyList<JourneyAppDto> Apps);

public record JourneyRequest(
    string? Name, string? Description, string? Level, string? SquadId, string? ParentId, IReadOnlyCollection<string>? Tags);

public record JourneyStepRequest(string? AppId, string? Method, string? Path, string? Label, string? Note);

/// <param name="Steps">A lista inteira, em ordem; substitui a anterior. Lista vazia remove todos.</param>
public record ReplaceJourneyStepsRequest(IReadOnlyList<JourneyStepRequest>? Steps);

/// <param name="Comparison"><c>comparado</c> | <c>primeira-analise</c> | <c>sem-contrato</c>: sem comparação não é "sem impacto".</param>
/// <param name="EndpointRemoved">O endpoint saiu do contrato.</param>
public record JourneyImpactStepDto(
    string Id, string JourneyName, int Position, string AppId, string AppName, string Method, string Path, string? Label, string Comparison,
    bool EndpointRemoved, IReadOnlyList<ContractChangeDto> Changes);

public record JourneyImpactDto(
    string JourneyId, int Breaking, int Attention, int Safe, int BreakingSteps, int StepsWithoutComparison,
    IReadOnlyList<JourneyImpactStepDto> Steps, IReadOnlyList<string> Warnings);
