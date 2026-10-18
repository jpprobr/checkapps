namespace CheckApps.Api.Contracts;

// Resultado do scan e estado de disponibilidade. Nada aqui carrega valores de configuração:
// o "problem" é um texto que diz O QUE configurar, nunca COM QUE valor.

public record ScanStatusDto(bool Enabled, string Source, bool Ready, string? Problem, int MaxResults, string SiglaPattern);

public record ScanRequest(IReadOnlyList<string>? Siglas, bool DryRun);

/// <param name="InCatalog">Itens do resultado que já viraram app; sobrepõe-se às demais contagens, não as soma.</param>
public record ScanCountsDto(int New, int Updated, int Unchanged, int InCatalog);

public record CatalogAppDto(string AppId, string AppName, string? SquadId = null, string? SquadName = null);

public record ScanItemDto(
    string Id, string Name, string? Url, string Sigla, string? SiglaApp, bool Archived, DateTimeOffset? PushedAt,
    string Outcome, SuggestionDto Suggestion, CatalogAppDto? CatalogApp);

public record ScanReportDto(bool DryRun, ScanCountsDto Counts, IReadOnlyList<ScanItemDto> Items, IReadOnlyList<string> Warnings);
