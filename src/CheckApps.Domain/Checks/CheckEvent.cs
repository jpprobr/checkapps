namespace CheckApps.Domain.Checks;

/// <summary>
/// Entrada do histórico de uma checagem. Imutável e só de acréscimo, como o
/// QualitySnapshot: registra um fato passado, nunca é editada. Sem "quem fez" até
/// haver autenticação (ADR-007).
/// </summary>
public class CheckEvent
{
    public required string Id { get; init; }
    public required string CheckId { get; init; }
    public string? AppId { get; init; }
    public required CheckEventType Type { get; init; }
    public CheckItemStatus? FromStatus { get; init; }
    public CheckItemStatus? ToStatus { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
}

public enum CheckEventType
{
    Closed,
    Reopened,
    ItemAdded,
    ItemRemoved,
    ItemStatusChanged
}
