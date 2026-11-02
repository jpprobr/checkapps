namespace CheckApps.Domain.Analysis;

/// <summary>
/// Decisão **humana** sobre um host (spec 017): "o host <see cref="HostKey"/> é o app <see cref="AppId"/>", que **recebe**
/// a chamada. Não pertence a nenhuma análise: reanalisar não altera nem apaga vínculos. A chave é <c>host[:porta]</c> em
/// minúsculas e pode ser um **modelo** com <c>{env}</c>.
/// </summary>
public sealed record HostLink
{
    public required string Id { get; init; }
    public required string HostKey { get; init; }
    public required string AppId { get; init; }

    /// <summary>Um de <see cref="HostLinkStates"/>.</summary>
    public required string State { get; init; }

    /// <summary>Um de <see cref="HostLinkOrigins"/>.</summary>
    public string Origin { get; init; } = HostLinkOrigins.Human;

    public required DateTimeOffset DecidedAt { get; init; }
    public string? Note { get; init; }
}

public static class HostLinkStates
{
    /// <summary>A pessoa disse que o host é o app.</summary>
    public const string Confirmed = "confirmado";

    /// <summary>A pessoa disse que o host NÃO é o app.</summary>
    public const string Rejected = "rejeitado";

    /// <summary>Reservado para sugestão futura (por nome ou IA). Nada o produz na spec 017.</summary>
    public const string Suggested = "sugerido";

    /// <summary>Os estados que uma pessoa pode gravar.</summary>
    public static readonly string[] Decidable = [Confirmed, Rejected];
}

public static class HostLinkOrigins
{
    public const string Human = "HUMAN";
}
