using CheckApps.Api.Contracts;
using CheckApps.Application;
using CheckApps.Application.Journeys;
using Microsoft.AspNetCore.Mvc;

namespace CheckApps.Api.Controllers;

/// <summary>
/// Jornadas em árvore (spec 022): cadastro, passos, resolução dos passos e impacto das mudanças de contrato. Os contratos só são lidos:
/// a análise roda pela ferramenta <c>CheckApps.Analyze</c>, nunca por aqui. Convenção de erro do projeto: entrada inválida → 400,
/// jornada da URL inexistente → 404, regra de estrutura → 409.
/// </summary>
[ApiController]
[Route("api/journeys")]
public class JourneysController(JourneyService journeyService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<JourneySummaryDto>>> List(CancellationToken cancellationToken) =>
        Ok((await journeyService.ListAsync(cancellationToken)).Select(s => s.ToDto()).ToList());

    [HttpGet("{journeyId}")]
    public Task<ActionResult<JourneyDetailDto>> Get(string journeyId, CancellationToken cancellationToken) =>
        Handle<JourneyDetailDto>(async () => Ok((await journeyService.GetDetailAsync(journeyId, cancellationToken)).ToDto()));

    [HttpPost]
    public Task<ActionResult<JourneyDto>> Create([FromBody] JourneyRequest request, CancellationToken cancellationToken) =>
        Handle<JourneyDto>(async () =>
        {
            var journey = await journeyService.CreateAsync(ToInput(request), cancellationToken);
            return CreatedAtAction(nameof(Get), new { journeyId = journey.Id }, journey.ToDto());
        });

    [HttpPut("{journeyId}")]
    public Task<ActionResult<JourneyDto>> Update(string journeyId, [FromBody] JourneyRequest request, CancellationToken cancellationToken) =>
        Handle<JourneyDto>(async () => Ok((await journeyService.UpdateAsync(journeyId, ToInput(request), cancellationToken)).ToDto()));

    [HttpDelete("{journeyId}")]
    public Task<ActionResult<object>> Delete(string journeyId, CancellationToken cancellationToken) =>
        Handle<object>(async () =>
        {
            await journeyService.DeleteAsync(journeyId, cancellationToken);
            return NoContent();
        });

    /// <summary>Substitui a lista inteira de passos e devolve a jornada com os passos já resolvidos.</summary>
    [HttpPut("{journeyId}/steps")]
    public Task<ActionResult<JourneyDetailDto>> ReplaceSteps(
        string journeyId, [FromBody] ReplaceJourneyStepsRequest request, CancellationToken cancellationToken) =>
        Handle<JourneyDetailDto>(async () =>
        {
            var steps = (request.Steps ?? throw new ArgumentException("Informe a lista de passos (pode ser vazia)."))
                .Select(s => new StepInput(s.AppId, s.Method, s.Path, s.Label, s.Note))
                .ToList();

            await journeyService.ReplaceStepsAsync(journeyId, steps, cancellationToken);
            return Ok((await journeyService.GetDetailAsync(journeyId, cancellationToken)).ToDto());
        });

    [HttpGet("{journeyId}/impact")]
    public Task<ActionResult<JourneyImpactDto>> Impact(string journeyId, CancellationToken cancellationToken) =>
        Handle<JourneyImpactDto>(async () => Ok((await journeyService.GetImpactAsync(journeyId, cancellationToken)).ToDto()));

    private static JourneyInput ToInput(JourneyRequest request) =>
        new(request.Name, request.Description, request.Level, request.SquadId, request.ParentId, request.Tags);

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
