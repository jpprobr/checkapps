using Dapper;
using CheckApps.Application.Persistence;
using CheckApps.Domain;
using Microsoft.Data.Sqlite;

namespace CheckApps.Infrastructure.Sqlite;

public class SquadRepository(SqliteConnectionFactory connectionFactory) : ISquadRepository
{
    private const string SelectColumns = "id AS Id, name AS Name, description AS Description, status AS Status";

    public async Task<IReadOnlyList<Squad>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var squads = await connection.QueryAsync<Squad>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM squads ORDER BY id", cancellationToken: cancellationToken));
        return squads.ToList();
    }

    public async Task<Squad?> GetAsync(string squadId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        return await connection.QuerySingleOrDefaultAsync<Squad>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM squads WHERE id = @squadId",
            new { squadId }, cancellationToken: cancellationToken));
    }

    public async Task SaveAsync(Squad squad, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO squads (id, name, description, status)
            VALUES (@Id, @Name, @Description, @Status)
            ON CONFLICT (id) DO UPDATE SET
                name = excluded.name,
                description = excluded.description,
                status = excluded.status
            """,
            squad, cancellationToken: cancellationToken));
    }

    public async Task<bool> TryCreateAsync(Squad squad, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO squads (id, name, description, status) VALUES (@Id, @Name, @Description, @Status)",
                squad, cancellationToken: cancellationToken));
            return true;
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode is SqliteErrorCodes.ConstraintPrimaryKey or SqliteErrorCodes.ConstraintUnique)
        {
            return false;
        }
    }

    public async Task DeleteAsync(string squadId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM squads WHERE id = @squadId", new { squadId }, cancellationToken: cancellationToken));
    }
}
