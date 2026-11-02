using CheckApps.Application.Persistence;
using CheckApps.Domain.Analysis;
using Dapper;

namespace CheckApps.Infrastructure.Sqlite;

/// <summary>SQL explícito e portável (ADR-009). A chave do host é gravada e comparada em minúsculas.</summary>
public class HostLinkRepository(SqliteConnectionFactory connectionFactory) : IHostLinkRepository
{
    private const string Columns =
        "id AS Id, host_key AS HostKey, app_id AS AppId, state AS State, origin AS Origin, decided_at AS DecidedAt, note AS Note";

    public async Task<IReadOnlyList<HostLink>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(
            $"SELECT {Columns} FROM host_links ORDER BY host_key, app_id", cancellationToken: cancellationToken));
        return rows.Select(r => r.ToLink()).ToList();
    }

    public async Task<HostLink?> GetAsync(string hostKey, string appId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            $"SELECT {Columns} FROM host_links WHERE host_key = @HostKey AND app_id = @AppId",
            new { HostKey = hostKey.ToLowerInvariant(), AppId = appId }, cancellationToken: cancellationToken));
        return row?.ToLink();
    }

    public async Task UpsertAsync(HostLink link, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var parameters = new
        {
            link.Id,
            HostKey = link.HostKey.ToLowerInvariant(),
            link.AppId,
            link.State,
            link.Origin,
            DecidedAt = IsoTimestamp.Format(link.DecidedAt),
            link.Note
        };

        // Portável: troca por UPDATE e, se nada mudou, INSERT (o id da linha existente é preservado).
        var updated = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE host_links SET state = @State, origin = @Origin, decided_at = @DecidedAt, note = @Note " +
            "WHERE host_key = @HostKey AND app_id = @AppId",
            parameters, cancellationToken: cancellationToken));

        if (updated == 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO host_links (id, host_key, app_id, state, origin, decided_at, note) " +
                "VALUES (@Id, @HostKey, @AppId, @State, @Origin, @DecidedAt, @Note)",
                parameters, cancellationToken: cancellationToken));
        }
    }

    public async Task<bool> DeleteAsync(string hostKey, string appId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var deleted = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM host_links WHERE host_key = @HostKey AND app_id = @AppId",
            new { HostKey = hostKey.ToLowerInvariant(), AppId = appId }, cancellationToken: cancellationToken));
        return deleted > 0;
    }

    private sealed class Row
    {
        public string Id { get; set; } = "";
        public string HostKey { get; set; } = "";
        public string AppId { get; set; } = "";
        public string State { get; set; } = "";
        public string Origin { get; set; } = "";
        public string DecidedAt { get; set; } = "";
        public string? Note { get; set; }

        public HostLink ToLink() => new()
        {
            Id = Id, HostKey = HostKey, AppId = AppId, State = State, Origin = Origin,
            DecidedAt = IsoTimestamp.Parse(DecidedAt), Note = Note
        };
    }
}
