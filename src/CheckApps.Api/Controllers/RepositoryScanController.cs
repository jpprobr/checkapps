using CheckApps.Api.Contracts;
using CheckApps.Application.Discovery;
using Microsoft.AspNetCore.Mvc;

namespace CheckApps.Api.Controllers;

/// <summary>
/// Dispara o scan de repositórios pela API (spec 006) — o mesmo <see cref="ScanService"/> da ferramenta
/// de linha de comando. **Desligado por padrão** (<c>Scan:Enabled</c>): sem autenticação (ADR-007), quem
/// abre o sistema poderia disparar scans com o token configurado, então ligar é decisão de quem opera, e
/// antes de qualquer uso compartilhado o endpoint exige autenticação.
/// Erros: sigla inválida ou limite de resultados → 400; desligado, não configurado ou já em andamento →
/// 409; falha da fonte (token, permissão, limite de chamadas, rede) → 502, com mensagem já saneada.
/// </summary>
[ApiController]
[Route("api/discovered-repositories/scan")]
public class RepositoryScanController(ScanService scanService, ScanOptions options) : ControllerBase
{
    // Um scan por vez, para dois cliques ou duas pessoas não gastarem o limite de chamadas do GitHub em paralelo.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    [HttpGet("status")]
    public ActionResult<ScanStatusDto> GetStatus()
    {
        var problem = scanService.ConfigurationProblem;
        return Ok(new ScanStatusDto(
            options.Enabled, options.Source.ToLowerInvariant(), Ready: options.Enabled && problem is null,
            problem, options.MaxResults, options.SiglaPattern));
    }

    [HttpPost]
    public async Task<ActionResult<ScanReportDto>> Scan([FromBody] ScanRequest request, CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            return Conflict("O scan pela API está desligado. Ligue com Scan:Enabled=true (ver docs/leitor-github.md).");
        }

        if (!await Gate.WaitAsync(0, cancellationToken))
        {
            return Conflict("Já há um scan em andamento. Aguarde terminar.");
        }

        try
        {
            var report = await scanService.ScanAsync(request.Siglas, request.DryRun, cancellationToken);
            return Ok(report.ToDto());
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ScanLimitExceededException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // Só o ScanService a lança, e só para configuração faltando (convenção de nome, token, organização).
            return Conflict(ex.Message);
        }
        catch (RepositorySourceException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, ex.Message);
        }
        finally
        {
            Gate.Release();
        }
    }
}
