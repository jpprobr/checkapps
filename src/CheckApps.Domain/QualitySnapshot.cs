namespace CheckApps.Domain;

public class QualitySnapshot
{
    public required string Id { get; init; }
    public required string AppId { get; init; }
    public required string SonarProjectId { get; init; }
    public required DateTimeOffset CollectedAt { get; init; }
    public required string Source { get; init; }
    public required QualityMetrics Metrics { get; init; }
    public required QualityLevel QualityLevel { get; init; }
    public required string AssessmentRulesVersion { get; init; }
}
