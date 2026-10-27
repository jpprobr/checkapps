using CheckApps.Api.Contracts;
using CheckApps.Application.Analysis;
using Microsoft.AspNetCore.Mvc;

namespace CheckApps.Api.Controllers;

/// <summary>
/// Leitura das análises de repositório (spec 013): dependências de um app, "quem usa o pacote X" e a versão do .NET por
/// squad. **Só lê** — a análise roda pela ferramenta <c>CheckApps.Analyze</c>, nunca por aqui, então nenhum endpoint
/// exige credencial do provedor nem gasta limite de chamadas. Convenção de erro: app inexistente → 404; nome do pacote
/// ausente → 400.
/// </summary>
[ApiController]
public class DependenciesController(DependencyService dependencyService) : ControllerBase
{
    [HttpGet("api/apps/{appId}/dependencies")]
    public async Task<ActionResult<AppDependenciesDto>> ForApp(string appId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok((await dependencyService.GetForAppAsync(appId, cancellationToken)).ToDto());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <param name="name">Nome exato do pacote, sem diferença de caixa. Obrigatório, salvo com <paramref name="internalOnly"/>.</param>
    /// <param name="internalOnly">Só pacotes internos (exige o prefixo configurado); sem nome, lista os pacotes internos em uso.</param>
    [HttpGet("api/dependencies/packages")]
    public async Task<ActionResult<PackageSearchResultDto>> SearchPackages(
        [FromQuery] string? name, [FromQuery] string? squadId, [FromQuery] bool internalOnly, CancellationToken cancellationToken)
    {
        try
        {
            return Ok((await dependencyService.SearchPackagesAsync(name, squadId, internalOnly, cancellationToken)).ToDto());
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("api/dependencies/frameworks")]
    public async Task<ActionResult<FrameworkOverviewDto>> Frameworks(CancellationToken cancellationToken) =>
        Ok((await dependencyService.GetFrameworkOverviewAsync(cancellationToken)).ToDto());
}
