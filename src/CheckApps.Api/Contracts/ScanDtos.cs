namespace CheckApps.Api.Contracts;

// Resultado do scan e estado de disponibilidade. Nada aqui carrega valores de configuração:
// o "problem" é um texto que diz O QUE configurar, nunca COM QUE valor.

public record ScanStatusDto(bool Enabled, string Source, bool Ready, string? Problem, int MaxResults, string SiglaPattern);

public record ScanRequest(IReadOnlyList<string>? Siglas, bool DryRun);

public record ScanCountsDto(int New, int Updated, int Unchanged);

public record ScanItemDto(
    string Id, string Name, string? Url, string Sigla, string? SiglaApp, bool Archived, DateTimeOffset? PushedAt,
    string Outcome, SuggestionDto Suggestion);

public record ScanReportDto(bool DryRun, ScanCountsDto Counts, IReadOnlyList<ScanItemDto> Items, IReadOnlyList<string> Warnings);
