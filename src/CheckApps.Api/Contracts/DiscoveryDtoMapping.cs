using CheckApps.Application.Discovery;
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
            new SuggestionDto(
                item.Suggestion.Role,
                item.Suggestion.Name,
                item.Suggestion.Confidence == SuggestionConfidence.High ? "high" : "low"));
    }
}
