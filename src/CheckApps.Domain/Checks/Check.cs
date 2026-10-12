namespace CheckApps.Domain.Checks;

/// <summary>
/// Lista de trabalho do time para uma demanda que atinge vários apps (spec 003).
/// Transversal: não pertence a uma squad.
/// </summary>
public class Check
{
    public required string Id { get; init; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Author { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public CheckStatus Status { get; set; } = CheckStatus.Open;
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ClosedAt { get; set; }
}

public enum CheckStatus
{
    Open,
    Closed
}
