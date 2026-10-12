namespace CheckApps.Api.Contracts;

// Status e tipos de evento circulam como os códigos do glossário (CheckCodes):
// "pendente", "em-andamento", "item-added"… — não como nomes de enum.

public record CheckProgressDto(int Total, int Done, double? Percent, IReadOnlyDictionary<string, int> CountsByStatus);

public record CheckSummaryDto(
    string Id, string Title, string? Category, string? Author, DateOnly? StartDate, DateOnly? EndDate,
    string Status, DateTimeOffset CreatedAt, DateTimeOffset? ClosedAt, CheckProgressDto Progress);

public record CheckItemDto(
    string AppId, string AppName, string SquadId, string SquadName, string? AppStatus,
    string Status, string? Notes, DateTimeOffset UpdatedAt);

public record CheckDetailDto(
    string Id, string Title, string? Description, string? Category, string? Author,
    DateOnly? StartDate, DateOnly? EndDate, string Status, DateTimeOffset CreatedAt, DateTimeOffset? ClosedAt,
    CheckProgressDto Progress, IReadOnlyList<CheckItemDto> Items);

public record AppCheckDto(
    string CheckId, string Title, string CheckStatus, DateTimeOffset? ClosedAt, string ItemStatus, string? Notes);

public record CheckCandidateDto(
    string AppId, string Name, string SquadId, string SquadName, string? Status, IReadOnlyCollection<string> Tags);

public record CheckEventDto(
    string Id, string? AppId, string Type, string? FromStatus, string? ToStatus, DateTimeOffset OccurredAt);

public record UpsertCheckRequest(
    string Title, string? Description, string? Category, string? Author, DateOnly? StartDate, DateOnly? EndDate);

public record AddCheckAppsRequest(IReadOnlyCollection<string> AppIds);

public record AddCheckAppsResultDto(int Added);

public record UpdateCheckItemRequest(string Status, string? Notes);
