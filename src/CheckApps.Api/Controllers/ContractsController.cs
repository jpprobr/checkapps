using CheckApps.Api.Contracts;
using CheckApps.Application.Analysis;
using Microsoft.AspNetCore.Mvc;

namespace CheckApps.Api.Controllers;

/// <summary>
/// Leitura dos contratos (spec 021): os endpoints declarados de um app e o que mudou desde o commit analisado anteriormente.
/// **Só lê**: a análise roda pela ferramenta <c>CheckApps.Analyze</c>, nunca por aqui, então nenhum endpoint exige credencial do
/// provedor. App inexistente → 404.
/// </summary>
[ApiController]
public class ContractsController(ContractService contractService) : ControllerBase
{
    [HttpGet("api/apps/{appId}/contracts")]
    public async Task<ActionResult<AppContractsDto>> ForApp(string appId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok((await contractService.GetForAppAsync(appId, cancellationToken)).ToDto());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("api/apps/{appId}/contracts/changes")]
    public async Task<ActionResult<AppContractChangesDto>> ChangesForApp(string appId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok((await contractService.GetChangesForAppAsync(appId, cancellationToken)).ToDto());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}
