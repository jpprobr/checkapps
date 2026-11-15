using CheckApps.Domain;

namespace CheckApps.Application.Journeys;

/// <summary>Um nó da lista de jornadas, com o que a árvore mostra sem abrir cada uma.</summary>
public sealed record JourneySummary(Journey Journey, string? SquadName, int StepCount, int ChildCount);

/// <summary>Estado de um passo contra o contrato corrente do app. Texto na API; a tela mostra em texto, nunca só por cor.</summary>
public static class StepResolutions
{
    /// <summary>O endpoint está no contrato corrente do app.</summary>
    public const string Cataloged = "catalogado";

    /// <summary>O app tem contrato analisado e o endpoint não está nele.</summary>
    public const string OutsideContract = "fora-do-contrato";

    /// <summary>O app nunca teve um contrato analisado: não dá para dizer.</summary>
    public const string AppWithoutContract = "app-sem-contrato";
}

/// <summary>O endpoint do contrato que o passo encontrou.</summary>
public sealed record ResolvedEndpoint(
    string? Summary, IReadOnlyList<string> Security, bool Deprecated, string Confidence, string RepositoryName, string SourceFile);

public sealed record JourneyStepView(JourneyStep Step, string AppName, string Resolution, ResolvedEndpoint? Endpoint);

/// <summary>Um app que a jornada usa, derivado dos passos da subárvore dela.</summary>
public sealed record JourneyApp(string AppId, string AppName, int StepCount);

/// <param name="Path">Os ancestrais, da raiz até o pai.</param>
public sealed record JourneyDetail(
    Journey Journey, string? SquadName, IReadOnlyList<Journey> Path, IReadOnlyList<JourneySummary> Children,
    IReadOnlyList<JourneyStepView> Steps, IReadOnlyList<JourneyApp> Apps);

/// <summary>Campos editáveis de uma jornada.</summary>
public sealed record JourneyInput(
    string? Name, string? Description, string? Level, string? SquadId, string? ParentId, IReadOnlyCollection<string>? Tags);

/// <summary>Um passo como a pessoa o registra.</summary>
public sealed record StepInput(string? AppId, string? Method, string? Path, string? Label, string? Note);
