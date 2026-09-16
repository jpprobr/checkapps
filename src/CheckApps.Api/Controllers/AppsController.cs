using CheckApps.Api.Contracts;
using CheckApps.Application;
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

    [HttpPost]
    public async Task<ActionResult<AppDetailDto>> Create([FromBody] CreateAppRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var app = await appService.CreateAsync(
                request.Name, request.SquadId, request.Description, request.Criticality,
                request.Tags, request.ExternalId, cancellationToken);
            return CreatedAtAction(nameof(Get), new { appId = app.Id }, app.ToDetailDto());
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{appId}")]
    public async Task<ActionResult<AppDetailDto>> Update(
        string appId, [FromBody] UpdateAppRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var app = await appService.UpdateAsync(
                appId, request.Name, request.SquadId, request.Description, request.Criticality,
                request.Tags, request.Status, request.ExternalId, cancellationToken);
            return Ok(app.ToDetailDto());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpDelete("{appId}")]
    public async Task<IActionResult> Delete(string appId, CancellationToken cancellationToken)
    {
        try
        {
            await appService.DeleteAsync(appId, cancellationToken);
            return NoContent();
        }
        catch (DomainConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpGet("{appId}/links")]
    public async Task<ActionResult<IReadOnlyList<AppLinkDto>>> ListLinks(string appId, CancellationToken cancellationToken)
    {
        var links = await appService.ListLinksAsync(appId, cancellationToken);
        return Ok(links.Select(l => l.ToDto()).ToList());
    }

    [HttpPost("{appId}/links")]
    public async Task<ActionResult<AppLinkDto>> AddLink(
        string appId, [FromBody] UpsertAppLinkRequest request, CancellationToken cancellationToken)
    {
        var link = await appService.AddLinkAsync(appId, request.Description, request.Uri, cancellationToken);
        return CreatedAtAction(nameof(ListLinks), new { appId }, link.ToDto());
    }

    [HttpPut("{appId}/links/{linkId}")]
    public async Task<ActionResult<AppLinkDto>> UpdateLink(
        string appId, string linkId, [FromBody] UpsertAppLinkRequest request, CancellationToken cancellationToken)
    {
        var link = await appService.UpdateLinkAsync(appId, linkId, request.Description, request.Uri, cancellationToken);
        return Ok(link.ToDto());
    }

    [HttpDelete("{appId}/links/{linkId}")]
    public async Task<IActionResult> DeleteLink(string appId, string linkId, CancellationToken cancellationToken)
    {
        await appService.DeleteLinkAsync(appId, linkId, cancellationToken);
        return NoContent();
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
