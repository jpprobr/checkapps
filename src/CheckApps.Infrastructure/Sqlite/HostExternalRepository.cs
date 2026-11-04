using CheckApps.Application.Persistence;
using CheckApps.Domain.Analysis;
using Dapper;

namespace CheckApps.Infrastructure.Sqlite;

/// <summary>SQL explícito e portável (ADR-009). A chave do host é gravada e comparada em minúsculas.</summary>
public class HostExternalRepository(SqliteConnectionFactory connectionFactory) : IHostExternalRepository
{
    private const string Columns =
        "id AS Id, host_key AS HostKey, name AS Name, origin AS Origin, decided_at AS DecidedAt, note AS Note";

    public async Task<IReadOnlyList<HostExternal>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(
            $"SELECT {Columns} FROM host_externals ORDER BY host_key", cancellationToken: cancellationToken));
        return rows.Select(r => r.ToExternal()).ToList();
    }

    public async Task UpsertAsync(HostExternal external, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var parameters = new
        {
            external.Id,
            HostKey = external.HostKey.ToLowerInvariant(),
            external.Name,
            external.Origin,
            DecidedAt = IsoTimestamp.Format(external.DecidedAt),
            external.Note
        };

        // Portável: troca por UPDATE e, se nada mudou, INSERT (o id da linha existente é preservado).
        var updated = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE host_externals SET name = @Name, origin = @Origin, decided_at = @DecidedAt, note = @Note " +
            "WHERE host_key = @HostKey",
            parameters, cancellationToken: cancellationToken));

        if (updated == 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO host_externals (id, host_key, name, origin, decided_at, note) " +
                "VALUES (@Id, @HostKey, @Name, @Origin, @DecidedAt, @Note)",
                parameters, cancellationToken: cancellationToken));
        }
    }

    public async Task<bool> DeleteAsync(string hostKey, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var deleted = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM host_externals WHERE host_key = @HostKey",
            new { HostKey = hostKey.ToLowerInvariant() }, cancellationToken: cancellationToken));
        return deleted > 0;
    }

    private sealed class Row
    {
        public string Id { get; set; } = "";
        public string HostKey { get; set; } = "";
        public string? Name { get; set; }
        public string Origin { get; set; } = "";
        public string DecidedAt { get; set; } = "";
        public string? Note { get; set; }

        public HostExternal ToExternal() => new()
        {
            Id = Id, HostKey = HostKey, Name = Name, Origin = Origin, DecidedAt = IsoTimestamp.Parse(DecidedAt), Note = Note
        };
    }
}
