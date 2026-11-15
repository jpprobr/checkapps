namespace CheckApps.Domain;

/// <summary>
/// Um nó da árvore de jornadas (spec 022): plataforma, sistema, jornada… O <see cref="Level"/> é texto livre, como o papel do
/// repositório: começa assim para avaliar com o uso (ADR-012). Só as jornadas-folha têm <see cref="JourneyStep"/>.
/// </summary>
public sealed record Journey
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }

    /// <summary>Texto livre (<c>plataforma</c>, <c>sistema</c>, <c>jornada</c>…), até <see cref="JourneyLimits.MaxLevelLength"/> caracteres.</summary>
    public string? Level { get; init; }

    /// <summary>Quem cuida. Opcional: plataforma e sistema podem atravessar squads.</summary>
    public string? SquadId { get; init; }

    public string? ParentId { get; init; }

    /// <summary>Classificação para pessoas (ex.: <c>tipo-produto:renda-fixa</c>); a convenção chave:valor é só orientação.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Um de <see cref="JourneySources"/>.</summary>
    public string Source { get; init; } = JourneySources.Manual;

    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Um endpoint de um app numa jornada-folha. Guarda a **identidade estável** do endpoint (app + método + chave da rota), e nunca
/// o id de um endpoint de análise, que é uma foto e muda a cada reanálise.
/// </summary>
public sealed record JourneyStep
{
    public required string Id { get; init; }
    public required string JourneyId { get; init; }

    /// <summary>Posição na jornada, a partir de zero.</summary>
    public required int Position { get; init; }

    public required string AppId { get; init; }

    /// <summary>Em maiúsculas (<c>POST</c>).</summary>
    public required string Method { get; init; }

    /// <summary>A rota como a pessoa a registrou (<c>/simulacoes/{id}</c>).</summary>
    public required string Path { get; init; }

    /// <summary>A rota com os parâmetros neutralizados (<see cref="Domain.Analysis.ApiPath.KeyOf"/>): é o que casa com o contrato.</summary>
    public required string PathKey { get; init; }

    /// <summary>Como a jornada chama esta etapa (ex.: "Simular a compra").</summary>
    public string? Label { get; init; }

    public string? Note { get; init; }
}

/// <summary>De onde veio a jornada. Hoje só cadastro manual.</summary>
public static class JourneySources
{
    public const string Manual = "MANUAL";
}

public static class JourneyLimits
{
    public const int MaxDepth = 8;
    public const int MaxNameLength = 120;
    public const int MaxLevelLength = 40;
    public const int MaxLabelLength = 120;
    public const int MaxNoteLength = 500;
    public const int MaxSteps = 100;
}
