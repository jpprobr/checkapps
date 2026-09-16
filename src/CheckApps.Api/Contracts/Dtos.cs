using CheckApps.Domain;

namespace CheckApps.Api.Contracts;

public record SquadDto(string Id, string Name, string? Description, string? Status);

public record AppSummaryDto(
    string Id,
    string SquadId,
    string Name,
    string? Criticality,
    string? Status,
    IReadOnlyCollection<string> Tags,
    QualityLevel? CurrentQualityLevel,
    DateTimeOffset? LastSnapshotAt);

public record QualityMetricsDto(
    double Coverage,
    int Bugs,
    int Vulnerabilities,
    int CodeSmells,
    double Duplications,
    string Reliability,
    string Maintainability,
    string Security);

public record AppDetailDto(
    string Id,
    string SquadId,
    string Name,
    string? Description,
    string? Criticality,
    IReadOnlyCollection<string> Tags,
    string? Status,
    string? ExternalId,
    QualityLevel? CurrentQualityLevel,
    QualityMetricsDto? CurrentMetrics,
    DateTimeOffset? LastSnapshotAt);

public record RepositoryDto(string Id, string Name, string? Provider, string? Url, string? Status);

public record SonarProjectDto(string Id, string ProjectKey, string? Name, string? Url, string? Status, string Source);

public record CurrentQualityDto(
    QualityLevel? QualityLevel,
    QualityMetricsDto? Metrics,
    DateTimeOffset? LastSnapshotAt,
    string? AssessmentRulesVersion);

public record QualitySnapshotDto(
    string Id,
    string SonarProjectId,
    DateTimeOffset CollectedAt,
    string Source,
    QualityMetricsDto Metrics,
    QualityLevel QualityLevel,
    string AssessmentRulesVersion);

public record AppQualityTrendDto(
    string AppId,
    string AppName,
    QualityLevel? PreviousLevel,
    QualityLevel CurrentLevel,
    string Direction);

public record SquadQualityEvolutionPointDto(DateTimeOffset Period, IReadOnlyDictionary<QualityLevel, int> LevelCounts);

public record SquadQualityEvolutionDto(
    IReadOnlyList<SquadQualityEvolutionPointDto> Points,
    IReadOnlyList<AppQualityTrendDto> AppTrends);

public record RegisterQualitySnapshotRequest(
    string SonarProjectId,
    QualityMetricsDto Metrics,
    DateTimeOffset CollectedAt,
    string Source);

public record CreateSquadRequest(string Name, string? Description);

public record UpdateSquadRequest(string Name, string? Description, string? Status);

public record CreateAppRequest(
    string Name,
    string SquadId,
    string? Description,
    string? Criticality,
    IReadOnlyCollection<string>? Tags,
    string? ExternalId);

public record UpdateAppRequest(
    string Name,
    string SquadId,
    string? Description,
    string? Criticality,
    IReadOnlyCollection<string>? Tags,
    string? Status,
    string? ExternalId);

public record AppLinkDto(string Id, string Description, string Uri);

public record UpsertAppLinkRequest(string Description, string Uri);
