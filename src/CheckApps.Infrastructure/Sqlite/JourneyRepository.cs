using CheckApps.Application.Persistence;
using CheckApps.Domain;
using Dapper;
using Microsoft.Data.Sqlite;

namespace CheckApps.Infrastructure.Sqlite;

/// <summary>SQL explícito e portável (ADR-009). Tags vêm em consulta separada, não por <c>GROUP_CONCAT</c>.</summary>
public class JourneyRepository(SqliteConnectionFactory connectionFactory) : IJourneyRepository
{
    private const string JourneyColumns =
        """
        id AS Id, name AS Name, description AS Description, level AS Level, squad_id AS SquadId, parent_id AS ParentId,
        source AS Source, created_at AS CreatedAt, updated_at AS UpdatedAt
        """;

    private const string StepColumns =
        """
        id AS Id, journey_id AS JourneyId, position AS Position, app_id AS AppId, method AS Method, path AS Path,
        path_key AS PathKey, label AS Label, note AS Note
        """;

    public async Task<IReadOnlyList<Journey>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var rows = (await connection.QueryAsync<JourneyRow>(new CommandDefinition(
            $"SELECT {JourneyColumns} FROM journeys ORDER BY id", cancellationToken: cancellationToken))).ToList();
        var tags = await LoadTagsAsync(connection, cancellationToken);
        return rows.Select(r => r.ToEntity(tags.GetValueOrDefault(r.Id) ?? [])).ToList();
    }

    public async Task<Journey?> GetAsync(string journeyId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var row = await connection.QuerySingleOrDefaultAsync<JourneyRow>(new CommandDefinition(
            $"SELECT {JourneyColumns} FROM journeys WHERE id = @journeyId", new { journeyId }, cancellationToken: cancellationToken));
        if (row is null)
        {
            return null;
        }

        var tags = (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT tag FROM journey_tags WHERE journey_id = @journeyId ORDER BY tag", new { journeyId }, cancellationToken: cancellationToken))).ToList();
        return row.ToEntity(tags);
    }

    public async Task<bool> TryCreateAsync(Journey journey, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        using var transaction = connection.BeginTransaction();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO journeys (id, name, description, level, squad_id, parent_id, source, created_at, updated_at)
                VALUES (@Id, @Name, @Description, @Level, @SquadId, @ParentId, @Source, @CreatedAt, @UpdatedAt)
                """,
                ToParameters(journey), transaction, cancellationToken: cancellationToken));
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode is SqliteErrorCodes.ConstraintPrimaryKey or SqliteErrorCodes.ConstraintUnique)
        {
            return false;
        }

        await ReplaceTagsAsync(connection, transaction, journey, cancellationToken);
        transaction.Commit();
        return true;
    }

    public async Task UpdateAsync(Journey journey, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        using var transaction = connection.BeginTransaction();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE journeys
            SET name = @Name, description = @Description, level = @Level, squad_id = @SquadId, parent_id = @ParentId,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """,
            ToParameters(journey), transaction, cancellationToken: cancellationToken));
        await ReplaceTagsAsync(connection, transaction, journey, cancellationToken);
        transaction.Commit();
    }

    public async Task DeleteAsync(string journeyId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM journeys WHERE id = @journeyId", new { journeyId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<JourneyStep>> GetStepsAsync(string journeyId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        return (await connection.QueryAsync<JourneyStep>(new CommandDefinition(
            $"SELECT {StepColumns} FROM journey_steps WHERE journey_id = @journeyId ORDER BY position",
            new { journeyId }, cancellationToken: cancellationToken))).ToList();
    }

    public async Task<IReadOnlyList<JourneyStep>> ListAllStepsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        return (await connection.QueryAsync<JourneyStep>(new CommandDefinition(
            $"SELECT {StepColumns} FROM journey_steps ORDER BY journey_id, position", cancellationToken: cancellationToken))).ToList();
    }

    public async Task ReplaceStepsAsync(string journeyId, IReadOnlyList<JourneyStep> steps, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        using var transaction = connection.BeginTransaction();
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM journey_steps WHERE journey_id = @journeyId", new { journeyId }, transaction, cancellationToken: cancellationToken));

        foreach (var step in steps)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO journey_steps (id, journey_id, position, app_id, method, path, path_key, label, note)
                VALUES (@Id, @JourneyId, @Position, @AppId, @Method, @Path, @PathKey, @Label, @Note)
                """,
                step, transaction, cancellationToken: cancellationToken));
        }

        transaction.Commit();
    }

    public async Task<int> CountOwnedBySquadAsync(string squadId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM journeys WHERE squad_id = @squadId", new { squadId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> AnyStepUsesAppAsync(string appId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM journey_steps WHERE app_id = @appId", new { appId }, cancellationToken: cancellationToken)) > 0;
    }

    private static object ToParameters(Journey journey) => new
    {
        journey.Id, journey.Name, journey.Description, journey.Level, journey.SquadId, journey.ParentId, journey.Source,
        CreatedAt = IsoTimestamp.Format(journey.CreatedAt), UpdatedAt = IsoTimestamp.Format(journey.UpdatedAt)
    };

    private static async Task<Dictionary<string, List<string>>> LoadTagsAsync(
        System.Data.IDbConnection connection, CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<(string JourneyId, string Tag)>(new CommandDefinition(
            "SELECT journey_id AS JourneyId, tag AS Tag FROM journey_tags ORDER BY tag", cancellationToken: cancellationToken));
        return rows.GroupBy(r => r.JourneyId).ToDictionary(g => g.Key, g => g.Select(r => r.Tag).ToList());
    }

    private static async Task ReplaceTagsAsync(
        System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, Journey journey, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM journey_tags WHERE journey_id = @journeyId", new { journeyId = journey.Id }, transaction, cancellationToken: cancellationToken));

        foreach (var tag in journey.Tags.Distinct(StringComparer.Ordinal))
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO journey_tags (journey_id, tag) VALUES (@journeyId, @tag)",
                new { journeyId = journey.Id, tag }, transaction, cancellationToken: cancellationToken));
        }
    }

    private sealed class JourneyRow
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? Level { get; set; }
        public string? SquadId { get; set; }
        public string? ParentId { get; set; }
        public string Source { get; set; } = "";
        public string CreatedAt { get; set; } = "";
        public string UpdatedAt { get; set; } = "";

        public Journey ToEntity(IReadOnlyList<string> tags) => new()
        {
            Id = Id, Name = Name, Description = Description, Level = Level, SquadId = SquadId, ParentId = ParentId,
            Tags = tags, Source = Source, CreatedAt = IsoTimestamp.Parse(CreatedAt), UpdatedAt = IsoTimestamp.Parse(UpdatedAt)
        };
    }
}
