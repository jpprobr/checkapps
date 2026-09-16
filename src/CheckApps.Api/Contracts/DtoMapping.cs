using CheckApps.Application.Squads;
using CheckApps.Domain;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.Api.Contracts;

public static class DtoMapping
{
    public static SquadDto ToDto(this Squad squad) => new(squad.Id, squad.Name, squad.Description, squad.Status);

    public static AppSummaryDto ToSummaryDto(this App app) => new(
        app.Id, app.SquadId, app.Name, app.Criticality, app.Status, app.Tags, app.CurrentQualityLevel, app.LastSnapshotAt);

    public static AppDetailDto ToDetailDto(this App app) => new(
        app.Id, app.SquadId, app.Name, app.Description, app.Criticality, app.Tags, app.Status, app.ExternalId,
        app.CurrentQualityLevel, app.CurrentMetrics?.ToDto(), app.LastSnapshotAt);

    public static CurrentQualityDto ToCurrentQualityDto(this App app) => new(
        app.CurrentQualityLevel, app.CurrentMetrics?.ToDto(), app.LastSnapshotAt, app.AssessmentRulesVersion);

    public static RepositoryDto ToDto(this Repository repository) => new(
        repository.Id, repository.Name, repository.Provider, repository.Url, repository.Status);

    public static SonarProjectDto ToDto(this SonarProject sonarProject) => new(
        sonarProject.Id, sonarProject.ProjectKey, sonarProject.Name, sonarProject.Url, sonarProject.Status, sonarProject.Source);

    public static QualityMetricsDto ToDto(this QualityMetrics metrics) => new(
        metrics.Coverage, metrics.Bugs, metrics.Vulnerabilities, metrics.CodeSmells, metrics.Duplications,
        metrics.Reliability, metrics.Maintainability, metrics.Security);

    public static QualityMetrics ToDomain(this QualityMetricsDto dto) => new()
    {
        Coverage = dto.Coverage,
        Bugs = dto.Bugs,
        Vulnerabilities = dto.Vulnerabilities,
        CodeSmells = dto.CodeSmells,
        Duplications = dto.Duplications,
        Reliability = dto.Reliability,
        Maintainability = dto.Maintainability,
        Security = dto.Security
    };

    public static QualitySnapshotDto ToDto(this QualitySnapshot snapshot) => new(
        snapshot.Id, snapshot.SonarProjectId, snapshot.CollectedAt, snapshot.Source,
        snapshot.Metrics.ToDto(), snapshot.QualityLevel, snapshot.AssessmentRulesVersion);

    public static SquadQualityEvolutionDto ToDto(this SquadQualityEvolution evolution) => new(
        [.. evolution.Points.Select(p => new SquadQualityEvolutionPointDto(p.Period, p.LevelCounts))],
        [.. evolution.AppTrends.Select(t => new AppQualityTrendDto(t.AppId, t.AppName, t.PreviousLevel, t.CurrentLevel, t.Direction.ToString()))]);

    public static AppLinkDto ToDto(this AppLink link) => new(link.Id, link.Description, link.Uri);
}
