namespace CheckApps.Domain;

public class App
{
    public required string Id { get; init; }
    public required string SquadId { get; init; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Criticality { get; set; }
    public IReadOnlyCollection<string> Tags { get; set; } = [];
    public IReadOnlyCollection<string> Links { get; set; } = [];
    public string? Status { get; set; }
    public string? ExternalId { get; set; }

    public QualityLevel? CurrentQualityLevel { get; set; }
    public QualityMetrics? CurrentMetrics { get; set; }
    public DateTimeOffset? LastSnapshotAt { get; set; }
    public string? LastSnapshotId { get; set; }
    public string? AssessmentRulesVersion { get; set; }
}
