namespace CheckApps.Domain;

public record QualityMetrics
{
    public required double Coverage { get; init; }
    public required int Bugs { get; init; }
    public required int Vulnerabilities { get; init; }
    public required int CodeSmells { get; init; }
    public required double Duplications { get; init; }
    public required string Reliability { get; init; }
    public required string Maintainability { get; init; }
    public required string Security { get; init; }
}
