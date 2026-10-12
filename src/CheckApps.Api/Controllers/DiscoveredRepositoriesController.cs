using CheckApps.Api.Contracts;
using CheckApps.Application;
using CheckApps.Application.Discovery;
using CheckApps.Domain.Discovery;
using Microsoft.AspNetCore.Mvc;

namespace CheckApps.Api.Controllers;

/// <summary>
/// Caixa de entrada do leitor de GitHub (spec 005). O scan grava os descobertos pela ferramenta de
/// linha de comando; aqui a pessoa decide. Convenção de erro: descoberto inexistente → 404; input
/// inválido (status desconhecido, nome vazio, squad ou app inexistente) → 400; regra de negócio
/// (já está no catálogo, sigla-app em uso) → 409.
/// </summary>
[ApiController]
[Route("api/discovered-repositories")]
public class DiscoveredRepositoriesController(TriageService triageService) : ControllerBase
{
    /// <param name="status">new (padrão), ignored, other-squad ou all.</param>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DiscoveredRepositoryDto>>> List(
        [FromQuery] string? status, [FromQuery] string? sigla, CancellationToken cancellationToken)
    {
        TriageStatus? filter;
        if (string.IsNullOrWhiteSpace(status))
        {
            filter = TriageStatus.New;
        }
        else if (status == "all")
        {
            filter = null;
        }
        else if (TriageCodes.TryParseStatus(status, out var parsed))
        {
            filter = parsed;
        }
        else
        {
            return BadRequest($"Status desconhecido: \"{status}\". Use new, ignored, other-squad ou all.");
        }

        var items = await triageService.ListAsync(filter, string.IsNullOrWhiteSpace(sigla) ? null : sigla, cancellationToken);
        return Ok(items.Select(i => i.ToDto()).ToList());
    }

    [HttpPost("{id}/create-app")]
    public Task<ActionResult<AppDetailDto>> CreateApp(
        string id, [FromBody] CreateAppFromDiscoveryRequest request, CancellationToken cancellationToken) =>
        Handle<AppDetailDto>(async () =>
        {
            var app = await triageService.CreateAppAsync(id, request.Name, request.SquadId, request.Description, request.Role, cancellationToken);
            return Created($"/api/apps/{app.Id}", app.ToDetailDto());
        });

    [HttpPost("{id}/link")]
    public Task<ActionResult<LinkResultDto>> Link(
        string id, [FromBody] LinkDiscoveryRequest request, CancellationToken cancellationToken) =>
        Handle<LinkResultDto>(async () =>
        {
            var warnings = await triageService.LinkAsync(id, request.AppId, request.Role, cancellationToken);
            return Ok(new LinkResultDto(warnings));
        });

    [HttpPost("{id}/other-squad")]
    public Task<ActionResult<object>> OtherSquad(string id, CancellationToken cancellationToken) =>
        Handle<object>(async () =>
        {
            await triageService.MarkOtherSquadAsync(id, cancellationToken);
            return NoContent();
        });

    [HttpPost("{id}/ignore")]
    public Task<ActionResult<object>> Ignore(string id, CancellationToken cancellationToken) =>
        Handle<object>(async () =>
        {
            await triageService.IgnoreAsync(id, cancellationToken);
            return NoContent();
        });

    [HttpPost("{id}/restore")]
    public Task<ActionResult<object>> Restore(string id, CancellationToken cancellationToken) =>
        Handle<object>(async () =>
        {
            await triageService.RestoreAsync(id, cancellationToken);
            return NoContent();
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
