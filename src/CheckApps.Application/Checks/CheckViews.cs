using CheckApps.Domain.Checks;

namespace CheckApps.Application.Checks;

/// <summary>Uma linha da lista de checagens: metadados + percentual (AP-1).</summary>
public record CheckSummary(Check Check, CheckProgress Progress);

/// <summary>Uma linha da "planilha": o item com nome, squad e status do app, por join (AP-2).</summary>
public record CheckItemView(
    string AppId, string AppName, string SquadId, string SquadName, string? AppStatus,
    CheckItemStatus Status, string? Notes, DateTimeOffset UpdatedAt);

/// <summary>Uma checagem vista a partir de um app, com o status dele nela (AP-3).</summary>
public record AppCheckEntry(
    string CheckId, string Title, CheckStatus CheckStatus, DateTimeOffset? ClosedAt,
    CheckItemStatus ItemStatus, string? Notes);

/// <summary>App que ainda não está na checagem, com o necessário para filtrar no seletor (AP-9).</summary>
public record CheckCandidateApp(
    string AppId, string Name, string SquadId, string SquadName, string? Status, IReadOnlyCollection<string> Tags);
