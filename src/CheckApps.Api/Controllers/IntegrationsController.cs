using CheckApps.Api.Contracts;
using CheckApps.Application;
using CheckApps.Application.Analysis;
using Microsoft.AspNetCore.Mvc;

namespace CheckApps.Api.Controllers;

/// <summary>
/// Leitura das integrações declaradas (spec 014): o que um app chama e quem chama cada host. **Só lê** — a análise roda
/// pela ferramenta <c>CheckApps.Analyze</c>, nunca por aqui, então nenhum endpoint exige credencial do provedor.
/// Convenção de erro: app ou host inexistente → 404.
/// </summary>
[ApiController]
public class IntegrationsController(IntegrationService integrationService, HostLinkService hostLinkService) : ControllerBase
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
    /// <param name="linkStatus"><c>sem-vinculo</c> | <c>confirmado</c> | <c>rejeitado</c> (spec 017); vazio traz todos; outro valor → 400.</param>
    [HttpGet("api/integrations/hosts")]
    public async Task<ActionResult<HostListDto>> Hosts(
        [FromQuery] string? host, [FromQuery] string? squadId, [FromQuery] string? linkStatus, CancellationToken cancellationToken)
    {
        try
        {
            return Ok((await integrationService.ListHostsAsync(host, squadId, linkStatus, cancellationToken)).ToDto());
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

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

    // ---- Vínculo host→app (spec 017): sempre decisão de uma pessoa --------------------------------------------------------

    /// <summary>Todos os vínculos, com o nome do app e se ainda são vistos nas análises correntes.</summary>
    [HttpGet("api/integrations/links")]
    public async Task<ActionResult<IReadOnlyList<HostLinkDto>>> Links(CancellationToken cancellationToken) =>
        Ok((await hostLinkService.ListAsync(cancellationToken)).Select(l => l.ToDto()).ToList());

    /// <summary>
    /// Confirma ou rejeita que o host é o app (cria ou troca o estado do par). 400 para host ou estado inválidos, 404 para
    /// app inexistente, 409 se o host já está confirmado para outro app.
    /// </summary>
    [HttpPut("api/integrations/hosts/{host}/link")]
    public async Task<ActionResult<HostLinkMatchDto>> SetLink(
        string host, [FromBody] SetHostLinkRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok((await hostLinkService.SetAsync(host, request.AppId, request.State, request.Note, cancellationToken)).ToDto());
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (DomainConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpDelete("api/integrations/hosts/{host}/link/{appId}")]
    public async Task<IActionResult> RemoveLink(string host, string appId, CancellationToken cancellationToken)
    {
        try
        {
            await hostLinkService.RemoveAsync(host, appId, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
