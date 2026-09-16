using CheckApps.Api.Contracts;
using CheckApps.Application;
using CheckApps.Application.Squads;
using Microsoft.AspNetCore.Mvc;

namespace CheckApps.Api.Controllers;

[ApiController]
[Route("api/squads")]
public class SquadsController(SquadService squadService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SquadDto>>> List(CancellationToken cancellationToken)
    {
        var squads = await squadService.ListSquadsAsync(cancellationToken);
        return Ok(squads.Select(s => s.ToDto()).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<SquadDto>> Create([FromBody] CreateSquadRequest request, CancellationToken cancellationToken)
    {
        var squad = await squadService.CreateAsync(request.Name, request.Description, cancellationToken);
        return CreatedAtAction(nameof(Get), new { squadId = squad.Id }, squad.ToDto());
    }

    [HttpPut("{squadId}")]
    public async Task<ActionResult<SquadDto>> Update(
        string squadId, [FromBody] UpdateSquadRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var squad = await squadService.UpdateAsync(squadId, request.Name, request.Description, request.Status, cancellationToken);
            return Ok(squad.ToDto());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpDelete("{squadId}")]
    public async Task<IActionResult> Delete(string squadId, CancellationToken cancellationToken)
    {
        try
        {
            await squadService.DeleteAsync(squadId, cancellationToken);
            return NoContent();
        }
        catch (DomainConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpGet("{squadId}")]
    public async Task<ActionResult<SquadDto>> Get(string squadId, CancellationToken cancellationToken)
    {
        var squad = await squadService.GetSquadAsync(squadId, cancellationToken);
        return squad is null ? NotFound() : Ok(squad.ToDto());
    }

    [HttpGet("{squadId}/apps")]
    public async Task<ActionResult<IReadOnlyList<AppSummaryDto>>> ListApps(string squadId, CancellationToken cancellationToken)
    {
        var apps = await squadService.ListAppsAsync(squadId, cancellationToken);
        return Ok(apps.Select(a => a.ToSummaryDto()).ToList());
    }

    [HttpGet("{squadId}/quality-evolution")]
    public async Task<ActionResult<SquadQualityEvolutionDto>> GetQualityEvolution(
        string squadId, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken cancellationToken)
    {
        var evolution = await squadService.GetQualityEvolutionAsync(squadId, from, to, cancellationToken);
        return Ok(evolution.ToDto());
    }
}
