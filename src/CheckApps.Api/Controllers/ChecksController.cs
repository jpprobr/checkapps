using CheckApps.Api.Contracts;
using CheckApps.Application;
using CheckApps.Application.Checks;
using CheckApps.Domain.Checks;
using Microsoft.AspNetCore.Mvc;

namespace CheckApps.Api.Controllers;

/// <summary>
/// Checagens (spec 003). Convenção de erro: checagem ou item inexistente → 404;
/// input inválido (título vazio, app inexistente na lista, status desconhecido) → 400;
/// regra de negócio → 409.
/// </summary>
[ApiController]
[Route("api/checks")]
public class ChecksController(CheckService checkService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CheckSummaryDto>>> List(
        [FromQuery] bool includeClosed, CancellationToken cancellationToken)
    {
        var checks = await checkService.ListAsync(includeClosed, cancellationToken);
        return Ok(checks.Select(c => c.ToDto()).ToList());
    }

    [HttpGet("{checkId}")]
    public async Task<ActionResult<CheckDetailDto>> Get(string checkId, CancellationToken cancellationToken)
    {
        var details = await checkService.GetAsync(checkId, cancellationToken);
        return details is null ? NotFound() : Ok(details.ToDto());
    }

    [HttpPost]
    public Task<ActionResult<CheckDetailDto>> Create([FromBody] UpsertCheckRequest request, CancellationToken cancellationToken) =>
        Handle<CheckDetailDto>(async () =>
        {
            var check = await checkService.CreateAsync(request.ToFields(), cancellationToken);
            var details = await checkService.GetAsync(check.Id, cancellationToken);
            return CreatedAtAction(nameof(Get), new { checkId = check.Id }, details!.ToDto());
        });

    [HttpPut("{checkId}")]
    public Task<ActionResult<CheckDetailDto>> Update(
        string checkId, [FromBody] UpsertCheckRequest request, CancellationToken cancellationToken) =>
        Handle<CheckDetailDto>(async () =>
        {
            await checkService.UpdateAsync(checkId, request.ToFields(), cancellationToken);
            return Ok((await checkService.GetAsync(checkId, cancellationToken))!.ToDto());
        });

    [HttpPost("{checkId}/close")]
    public Task<ActionResult<CheckDetailDto>> Close(string checkId, CancellationToken cancellationToken) =>
        Handle<CheckDetailDto>(async () =>
        {
            await checkService.CloseAsync(checkId, cancellationToken);
            return Ok((await checkService.GetAsync(checkId, cancellationToken))!.ToDto());
        });

    [HttpPost("{checkId}/reopen")]
    public Task<ActionResult<CheckDetailDto>> Reopen(string checkId, CancellationToken cancellationToken) =>
        Handle<CheckDetailDto>(async () =>
        {
            await checkService.ReopenAsync(checkId, cancellationToken);
            return Ok((await checkService.GetAsync(checkId, cancellationToken))!.ToDto());
        });

    [HttpDelete("{checkId}")]
    public Task<ActionResult<object>> Delete(string checkId, CancellationToken cancellationToken) =>
        Handle<object>(async () =>
        {
            await checkService.DeleteAsync(checkId, cancellationToken);
            return NoContent();
        });

    [HttpGet("{checkId}/candidates")]
    public Task<ActionResult<IReadOnlyList<CheckCandidateDto>>> ListCandidates(string checkId, CancellationToken cancellationToken) =>
        Handle<IReadOnlyList<CheckCandidateDto>>(async () =>
        {
            var candidates = await checkService.ListCandidatesAsync(checkId, cancellationToken);
            return Ok(candidates.Select(c => c.ToDto()).ToList());
        });

    [HttpPost("{checkId}/items")]
    public Task<ActionResult<AddCheckAppsResultDto>> AddApps(
        string checkId, [FromBody] AddCheckAppsRequest request, CancellationToken cancellationToken) =>
        Handle<AddCheckAppsResultDto>(async () =>
        {
            var added = await checkService.AddAppsAsync(checkId, request.AppIds, cancellationToken);
            return Ok(new AddCheckAppsResultDto(added));
        });

    [HttpPut("{checkId}/items/{appId}")]
    public Task<ActionResult<CheckDetailDto>> UpdateItem(
        string checkId, string appId, [FromBody] UpdateCheckItemRequest request, CancellationToken cancellationToken) =>
        Handle<CheckDetailDto>(async () =>
        {
            if (!CheckCodes.TryParseItemStatus(request.Status, out var status))
            {
                return BadRequest($"Status desconhecido: \"{request.Status}\".");
            }

            await checkService.UpdateItemAsync(checkId, appId, status, request.Notes, cancellationToken);
            return Ok((await checkService.GetAsync(checkId, cancellationToken))!.ToDto());
        });

    [HttpDelete("{checkId}/items/{appId}")]
    public Task<ActionResult<object>> RemoveApp(string checkId, string appId, CancellationToken cancellationToken) =>
        Handle<object>(async () =>
        {
            await checkService.RemoveAppAsync(checkId, appId, cancellationToken);
            return NoContent();
        });

    [HttpGet("{checkId}/history")]
    public Task<ActionResult<IReadOnlyList<CheckEventDto>>> GetHistory(string checkId, CancellationToken cancellationToken) =>
        Handle<IReadOnlyList<CheckEventDto>>(async () =>
        {
            var history = await checkService.GetHistoryAsync(checkId, cancellationToken);
            return Ok(history.Select(e => e.ToDto()).ToList());
        });

    private async Task<ActionResult<T>> Handle<T>(Func<Task<ActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (DomainConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }
}
