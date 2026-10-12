using CheckApps.Application.Discovery;
using CheckApps.Application.Persistence;
using CheckApps.Domain.Discovery;

namespace CheckApps.Api.Contracts;

public static class DiscoveryDtoMapping
{
    public static DiscoveredRepositoryDto ToDto(this DiscoveredItem item)
    {
        var r = item.Repository;
        return new DiscoveredRepositoryDto(
            r.Id, r.Provider, r.ExternalId, r.Name, r.Url, r.Sigla, r.SiglaApp, r.Archived, r.PushedAt,
            r.Status.ToCode(), r.FirstSeenAt, r.LastSeenAt, r.Source,
            item.Suggestion.ToDto());
    }

    public static SuggestionDto ToDto(this RepositorySuggestion suggestion) => new(
        suggestion.Role, suggestion.Name, suggestion.Confidence == SuggestionConfidence.High ? "high" : "low");

    public static ScanReportDto ToDto(this ScanReport report) => new(
        report.DryRun,
        new ScanCountsDto(report.Count(UpsertOutcome.New), report.Count(UpsertOutcome.Updated), report.Count(UpsertOutcome.Unchanged)),
        report.Items.Select(i => new ScanItemDto(
            i.Repository.Id, i.Repository.Name, i.Repository.Url, i.Repository.Sigla, i.Repository.SiglaApp,
            i.Repository.Archived, i.Repository.PushedAt, i.Outcome.ToCode(), i.Suggestion.ToDto())).ToList(),
        report.Warnings);

    private static string ToCode(this UpsertOutcome outcome) => outcome switch
    {
        UpsertOutcome.New => "new",
        UpsertOutcome.Updated => "updated",
        _ => "unchanged"
    };
}
