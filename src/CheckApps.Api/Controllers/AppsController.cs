using CheckApps.Api.Contracts;
using CheckApps.Application.Apps;
using CheckApps.Application.QualitySnapshots;
using CheckApps.Domain;
using Microsoft.AspNetCore.Mvc;

namespace CheckApps.Api.Controllers;

[ApiController]
[Route("api/apps")]
public class AppsController(AppService appService, QualitySnapshotService snapshotService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AppSummaryDto>>> ListByQualityLevel(
        [FromQuery] QualityLevel qualityLevel, CancellationToken cancellationToken)
    {
        var apps = await appService.ListByQualityLevelAsync(qualityLevel, cancellationToken);
        return Ok(apps.Select(a => a.ToSummaryDto()).ToList());
    }

    [HttpGet("{appId}")]
    public async Task<ActionResult<AppDetailDto>> Get(string appId, CancellationToken cancellationToken)
    {
        var app = await appService.GetAppAsync(appId, cancellationToken);
        return app is null ? NotFound() : Ok(app.ToDetailDto());
    }

    [HttpGet("{appId}/repositories")]
    public async Task<ActionResult<IReadOnlyList<RepositoryDto>>> ListRepositories(string appId, CancellationToken cancellationToken)
    {
        var repositories = await appService.ListRepositoriesAsync(appId, cancellationToken);
        return Ok(repositories.Select(r => r.ToDto()).ToList());
    }

    [HttpGet("{appId}/sonar-projects")]
    public async Task<ActionResult<IReadOnlyList<SonarProjectDto>>> ListSonarProjects(string appId, CancellationToken cancellationToken)
    {
        var sonarProjects = await appService.ListSonarProjectsAsync(appId, cancellationToken);
        return Ok(sonarProjects.Select(s => s.ToDto()).ToList());
    }

    [HttpGet("{appId}/quality")]
    public async Task<ActionResult<CurrentQualityDto>> GetCurrentQuality(string appId, CancellationToken cancellationToken)
    {
        var app = await appService.GetAppAsync(appId, cancellationToken);
        return app is null ? NotFound() : Ok(app.ToCurrentQualityDto());
    }

    [HttpGet("{appId}/quality/history")]
    public async Task<ActionResult<IReadOnlyList<QualitySnapshotDto>>> GetQualityHistory(
        string appId, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken cancellationToken)
    {
        var history = await appService.GetQualityHistoryAsync(appId, from, to, cancellationToken);
        return Ok(history.Select(s => s.ToDto()).ToList());
    }

    [HttpPost("{appId}/quality-snapshots")]
    public async Task<ActionResult<QualitySnapshotDto>> RegisterQualitySnapshot(
        string appId, [FromBody] RegisterQualitySnapshotRequest request, CancellationToken cancellationToken)
    {
        var snapshot = await snapshotService.RegisterAsync(
            appId, request.SonarProjectId, request.Metrics.ToDomain(), request.CollectedAt, request.Source, cancellationToken);

        return CreatedAtAction(nameof(GetQualityHistory), new { appId }, snapshot.ToDto());
    }
}
