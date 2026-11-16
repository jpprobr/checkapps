using CheckApps.Infrastructure.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace CheckApps.Api.Controllers;

/// <param name="Mode"><c>real</c> | <c>demo</c> (spec 023).</param>
/// <param name="Database">Só o nome do arquivo do banco, nunca o caminho.</param>
public record InfoDto(string Mode, string Database);

/// <summary>Em que modo a API está rodando e com qual banco (spec 023). A tela usa para mostrar a faixa do modo demonstração.</summary>
[ApiController]
[Route("api/info")]
public class InfoController(IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public ActionResult<InfoDto> Get() =>
        Ok(new InfoDto(CheckAppsMode.Read(configuration), Path.GetFileName(CheckAppsMode.DatabasePath(configuration))));
}
