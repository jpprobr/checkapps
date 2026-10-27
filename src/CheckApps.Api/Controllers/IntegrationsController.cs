using CheckApps.Api.Contracts;
using CheckApps.Application.Analysis;
using Microsoft.AspNetCore.Mvc;

namespace CheckApps.Api.Controllers;

/// <summary>
/// Leitura das integrações declaradas (spec 014): o que um app chama e quem chama cada host. **Só lê** — a análise roda
/// pela ferramenta <c>CheckApps.Analyze</c>, nunca por aqui, então nenhum endpoint exige credencial do provedor.
/// Convenção de erro: app ou host inexistente → 404.
/// </summary>
[ApiController]
public class IntegrationsController(IntegrationService integrationService) : ControllerBase
{
    [HttpGet("api/apps/{appId}/integrations")]
    public async Task<ActionResult<AppIntegrationsDto>> ForApp(string appId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok((await integrationService.GetForAppAsync(appId, cancellationToken)).ToDto());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <param name="host">Trecho do host, sem diferença de caixa; vazio lista todos.</param>
    [HttpGet("api/integrations/hosts")]
    public async Task<ActionResult<HostListDto>> Hosts(
        [FromQuery] string? host, [FromQuery] string? squadId, CancellationToken cancellationToken) =>
        Ok((await integrationService.ListHostsAsync(host, squadId, cancellationToken)).ToDto());

    /// <param name="host"><c>host</c> ou <c>host:porta</c>, como na lista (codificado na URL: o modelo tem <c>{env}</c>).</param>
    [HttpGet("api/integrations/hosts/{host}")]
    public async Task<ActionResult<HostDetailDto>> HostDetail(string host, CancellationToken cancellationToken)
    {
        try
        {
            return Ok((await integrationService.GetHostAsync(host, cancellationToken)).ToDto());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
