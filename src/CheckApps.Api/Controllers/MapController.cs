using CheckApps.Api.Contracts;
using CheckApps.Application.Analysis;
using Microsoft.AspNetCore.Mvc;

namespace CheckApps.Api.Controllers;

/// <summary>
/// Mapa de aplicações (spec 020): o grafo "quem chama quem" derivado das integrações declaradas e das decisões humanas
/// host→app. **Só lê.** Squad sem integrações devolve um grafo vazio (não é erro).
/// </summary>
[ApiController]
public class MapController(MapService mapService) : ControllerBase
{
    [HttpGet("api/map")]
    public async Task<ActionResult<AppMapDto>> Get([FromQuery] string? squadId, CancellationToken cancellationToken) =>
        Ok((await mapService.GetAsync(squadId, cancellationToken)).ToDto());
}
