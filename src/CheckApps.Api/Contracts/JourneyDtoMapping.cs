using CheckApps.Application.Journeys;
using CheckApps.Domain;

namespace CheckApps.Api.Contracts;

public static class JourneyDtoMapping
{
    public static JourneyDto ToDto(this Journey j) => new(
        j.Id, j.Name, j.Description, j.Level, j.SquadId, j.ParentId, j.Tags, j.Source, j.CreatedAt, j.UpdatedAt);

    public static JourneySummaryDto ToDto(this JourneySummary s) => new(
        s.Journey.Id, s.Journey.Name, s.Journey.Level, s.Journey.SquadId, s.SquadName, s.Journey.ParentId, s.Journey.Tags, s.StepCount, s.ChildCount);

    public static JourneyDetailDto ToDto(this JourneyDetail d) => new(
        d.Journey.ToDto(), d.SquadName, d.Path.Select(p => new JourneyPathItemDto(p.Id, p.Name, p.Level)).ToList(),
        d.Children.Select(c => c.ToDto()).ToList(), d.Steps.Select(s => s.ToDto()).ToList(),
        d.Apps.Select(a => new JourneyAppDto(a.AppId, a.AppName, a.StepCount)).ToList());

    private static JourneyStepDto ToDto(this JourneyStepView view) => new(
        view.Step.Id, view.Step.Position, view.Step.AppId, view.AppName, view.Step.Method, view.Step.Path, view.Step.Label, view.Step.Note,
        view.Resolution,
        view.Endpoint is { } e ? new ResolvedEndpointDto(e.Summary, e.Security, e.Deprecated, e.Confidence, e.RepositoryName, e.SourceFile) : null);

    public static JourneyImpactDto ToDto(this JourneyImpact impact) => new(
        impact.JourneyId, impact.Breaking, impact.Attention, impact.Safe, impact.BreakingSteps, impact.StepsWithoutComparison,
        impact.Steps.Select(s => new JourneyImpactStepDto(
            s.Step.Id, s.JourneyName, s.Step.Position, s.Step.AppId, s.AppName, s.Step.Method, s.Step.Path, s.Step.Label, s.Comparison,
            s.EndpointRemoved,
            s.Changes.Select(c => new ContractChangeDto(
                c.Severity, c.Kind, c.Method, c.Path, c.Location, c.StatusCode, c.MediaType, c.FieldPath, c.Description)).ToList())).ToList(),
        impact.Warnings);
}
