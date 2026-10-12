namespace CheckApps.Api.Contracts;

// Status e confiança circulam como códigos (new | ignored | other-squad; high | low), não como nomes de enum.

public record SuggestionDto(string? Role, string Name, string Confidence);

public record DiscoveredRepositoryDto(
    string Id, string Provider, string ExternalId, string Name, string? Url, string Sigla, string? SiglaApp,
    bool Archived, DateTimeOffset? PushedAt, string Status, DateTimeOffset FirstSeenAt, DateTimeOffset LastSeenAt,
    string Source, SuggestionDto Suggestion);

public record CreateAppFromDiscoveryRequest(string Name, string SquadId, string? Description, string? Role);

public record LinkDiscoveryRequest(string AppId, string? Role);

public record LinkResultDto(IReadOnlyList<string> Warnings);
