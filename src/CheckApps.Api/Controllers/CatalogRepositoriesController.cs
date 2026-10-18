using CheckApps.Api.Contracts;
using CheckApps.Application.Apps;
using Microsoft.AspNetCore.Mvc;

namespace CheckApps.Api.Controllers;

/// <summary>
/// Repositórios do catálogo que a caixa de entrada do scan não mostra: os cadastrados à mão e os do seed
/// (spec 010). Os que vieram do scan continuam em <c>api/discovered-repositories</c>.
/// </summary>
[ApiController]
[Route("api/repositories")]
public class CatalogRepositoriesController(AppService appService) : ControllerBase
{
    [HttpGet("catalog")]
    public async Task<ActionResult<IReadOnlyList<CatalogRepositoryDto>>> ListCatalogOnly(
        [FromQuery] string? sigla, CancellationToken cancellationToken)
    {
        var items = await appService.ListCatalogRepositoriesAsync(sigla, cancellationToken);
        return Ok(items.Select(i => new CatalogRepositoryDto(
            i.Repository.Id, i.Repository.Name, i.Repository.Provider, i.Repository.Url, i.Repository.Status,
            i.Repository.Sigla, i.Repository.SiglaApp, i.Repository.Role, i.Repository.Source,
            i.AppId, i.AppName, i.SquadId, i.SquadName)).ToList());
    }
}
