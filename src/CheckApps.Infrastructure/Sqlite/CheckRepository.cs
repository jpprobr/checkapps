using System.Data;
using System.Globalization;
using Dapper;
using CheckApps.Application.Checks;
using CheckApps.Application.Persistence;
using CheckApps.Domain.Checks;
using Microsoft.Data.Sqlite;

namespace CheckApps.Infrastructure.Sqlite;

public class CheckRepository(SqliteConnectionFactory connectionFactory) : ICheckRepository
{
    private const string SelectCheckColumns =
        """
        id AS Id, title AS Title, description AS Description, category AS Category, author AS Author,
        start_date AS StartDate, end_date AS EndDate, status AS Status,
        created_at AS CreatedAt, closed_at AS ClosedAt
        """;

    public async Task<bool> TryCreateAsync(Check check, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO checks (id, title, description, category, author, start_date, end_date, status, created_at, closed_at)
                VALUES (@Id, @Title, @Description, @Category, @Author, @StartDate, @EndDate, @Status, @CreatedAt, @ClosedAt)
                """,
                ToCheckParameters(check), cancellationToken: cancellationToken));
            return true;
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode is SqliteErrorCodes.ConstraintPrimaryKey or SqliteErrorCodes.ConstraintUnique)
        {
            return false;
        }
    }

    public async Task SaveAsync(Check check, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE checks SET
                title = @Title, description = @Description, category = @Category, author = @Author,
                start_date = @StartDate, end_date = @EndDate
            WHERE id = @Id
            """,
            ToCheckParameters(check), cancellationToken: cancellationToken));
    }

    public async Task<Check?> GetAsync(string checkId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var row = await connection.QuerySingleOrDefaultAsync<CheckRow>(new CommandDefinition(
            $"SELECT {SelectCheckColumns} FROM checks WHERE id = @checkId",
            new { checkId }, cancellationToken: cancellationToken));
        return row?.ToCheck();
    }

    /// <summary>
    /// Contagens em consulta separada (GROUP BY por checagem e status) e costuradas em C#,
    /// em vez de um GROUP BY sobre todas as colunas de checks: mais simples e portável.
    /// </summary>
    public async Task<IReadOnlyList<CheckSummary>> ListAsync(bool includeClosed, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var checks = (await connection.QueryAsync<CheckRow>(new CommandDefinition(
            $"""
            SELECT {SelectCheckColumns} FROM checks
            {(includeClosed ? "" : "WHERE status = @open")}
            ORDER BY created_at DESC, id
            """,
            new { open = CheckStatus.Open.ToCode() }, cancellationToken: cancellationToken)))
            .Select(r => r.ToCheck())
            .ToList();

        if (checks.Count == 0)
        {
            return [];
        }

        var counts = (await connection.QueryAsync<(string CheckId, string Status, long Count)>(new CommandDefinition(
            """
            SELECT check_id, status, COUNT(*) FROM check_items
            WHERE check_id IN @checkIds
            GROUP BY check_id, status
            """,
            new { checkIds = checks.Select(c => c.Id).ToList() }, cancellationToken: cancellationToken)))
            .GroupBy(r => r.CheckId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<CheckItemStatus, int>)g.ToDictionary(r => CheckCodes.ParseItemStatus(r.Status), r => (int)r.Count));

        return checks
            .Select(c => new CheckSummary(c, CheckProgress.From(counts.GetValueOrDefault(c.Id, new Dictionary<CheckItemStatus, int>()))))
            .ToList();
    }

    public async Task<IReadOnlyList<CheckItemView>> GetItemsAsync(string checkId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var rows = await connection.QueryAsync<CheckItemViewRow>(new CommandDefinition(
            """
            SELECT i.app_id AS AppId, a.name AS AppName, a.squad_id AS SquadId, s.name AS SquadName,
                   a.status AS AppStatus, i.status AS Status, i.notes AS Notes, i.updated_at AS UpdatedAt
            FROM check_items i
            JOIN apps a ON a.id = i.app_id
            JOIN squads s ON s.id = a.squad_id
            WHERE i.check_id = @checkId
            ORDER BY s.name, a.name
            """,
            new { checkId }, cancellationToken: cancellationToken));

        return rows.Select(r => new CheckItemView(
            r.AppId, r.AppName, r.SquadId, r.SquadName, r.AppStatus,
            CheckCodes.ParseItemStatus(r.Status), r.Notes, IsoTimestamp.Parse(r.UpdatedAt))).ToList();
    }

    public async Task<CheckItem?> GetItemAsync(string checkId, string appId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var row = await connection.QuerySingleOrDefaultAsync<CheckItemRow>(new CommandDefinition(
            """
            SELECT check_id AS CheckId, app_id AS AppId, status AS Status, notes AS Notes, updated_at AS UpdatedAt
            FROM check_items WHERE check_id = @checkId AND app_id = @appId
            """,
            new { checkId, appId }, cancellationToken: cancellationToken));

        return row is null ? null : new CheckItem
        {
            CheckId = row.CheckId,
            AppId = row.AppId,
            Status = CheckCodes.ParseItemStatus(row.Status),
            Notes = row.Notes,
            UpdatedAt = IsoTimestamp.Parse(row.UpdatedAt)
        };
    }

    public async Task<IReadOnlyList<AppCheckEntry>> ListByAppAsync(string appId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var rows = await connection.QueryAsync<AppCheckEntryRow>(new CommandDefinition(
            """
            SELECT c.id AS CheckId, c.title AS Title, c.status AS CheckStatus, c.closed_at AS ClosedAt,
                   i.status AS ItemStatus, i.notes AS Notes
            FROM check_items i
            JOIN checks c ON c.id = i.check_id
            WHERE i.app_id = @appId
            ORDER BY c.created_at DESC, c.id
            """,
            new { appId }, cancellationToken: cancellationToken));

        return rows.Select(r => new AppCheckEntry(
            r.CheckId, r.Title, CheckCodes.ParseStatus(r.CheckStatus),
            r.ClosedAt is null ? null : IsoTimestamp.Parse(r.ClosedAt),
            CheckCodes.ParseItemStatus(r.ItemStatus), r.Notes)).ToList();
    }

    public async Task<IReadOnlyList<CheckCandidateApp>> ListCandidateAppsAsync(string checkId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var rows = (await connection.QueryAsync<CandidateRow>(new CommandDefinition(
            """
            SELECT a.id AS AppId, a.name AS Name, a.squad_id AS SquadId, s.name AS SquadName, a.status AS Status
            FROM apps a
            JOIN squads s ON s.id = a.squad_id
            WHERE NOT EXISTS (
                SELECT 1 FROM check_items i WHERE i.check_id = @checkId AND i.app_id = a.id)
            ORDER BY s.name, a.name
            """,
            new { checkId }, cancellationToken: cancellationToken))).ToList();

        if (rows.Count == 0)
        {
            return [];
        }

        var tags = (await connection.QueryAsync<(string AppId, string Tag)>(new CommandDefinition(
            "SELECT app_id, tag FROM app_tags WHERE app_id IN @appIds ORDER BY tag",
            new { appIds = rows.Select(r => r.AppId).ToList() }, cancellationToken: cancellationToken)))
            .GroupBy(r => r.AppId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Tag).ToList());

        return rows.Select(r => new CheckCandidateApp(
            r.AppId, r.Name, r.SquadId, r.SquadName, r.Status, tags.GetValueOrDefault(r.AppId, []))).ToList();
    }

    public async Task<bool> AddItemAsync(CheckItem item, CheckEvent added, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        using var transaction = connection.BeginTransaction();

        var inserted = await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO check_items (check_id, app_id, status, notes, updated_at)
            VALUES (@CheckId, @AppId, @Status, @Notes, @UpdatedAt)
            ON CONFLICT (check_id, app_id) DO NOTHING
            """,
            ToItemParameters(item), transaction, cancellationToken: cancellationToken));

        if (inserted == 0)
        {
            return false;
        }

        await InsertEventAsync(connection, transaction, added, cancellationToken);
        transaction.Commit();
        return true;
    }

    public async Task RemoveItemAsync(string checkId, string appId, CheckEvent removed, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM check_items WHERE check_id = @checkId AND app_id = @appId",
            new { checkId, appId }, transaction, cancellationToken: cancellationToken));
        await InsertEventAsync(connection, transaction, removed, cancellationToken);

        transaction.Commit();
    }

    public async Task UpdateItemAsync(CheckItem item, CheckEvent? statusChanged, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE check_items SET status = @Status, notes = @Notes, updated_at = @UpdatedAt
            WHERE check_id = @CheckId AND app_id = @AppId
            """,
            ToItemParameters(item), transaction, cancellationToken: cancellationToken));

        if (statusChanged is not null)
        {
            await InsertEventAsync(connection, transaction, statusChanged, cancellationToken);
        }

        transaction.Commit();
    }

    public async Task SetStatusAsync(Check check, CheckEvent changed, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE checks SET status = @Status, closed_at = @ClosedAt WHERE id = @Id",
            ToCheckParameters(check), transaction, cancellationToken: cancellationToken));
        await InsertEventAsync(connection, transaction, changed, cancellationToken);

        transaction.Commit();
    }

    public async Task DeleteAsync(string checkId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM checks WHERE id = @checkId", new { checkId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> IsAppInAnyCheckAsync(string appId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM check_items WHERE app_id = @appId)",
            new { appId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<CheckEvent>> GetHistoryAsync(string checkId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var rows = await connection.QueryAsync<CheckEventRow>(new CommandDefinition(
            """
            SELECT id AS Id, check_id AS CheckId, app_id AS AppId, type AS Type,
                   from_status AS FromStatus, to_status AS ToStatus, occurred_at AS OccurredAt
            FROM check_events WHERE check_id = @checkId
            ORDER BY occurred_at DESC, id DESC
            """,
            new { checkId }, cancellationToken: cancellationToken));

        return rows.Select(r => new CheckEvent
        {
            Id = r.Id,
            CheckId = r.CheckId,
            AppId = r.AppId,
            Type = CheckCodes.ParseEventType(r.Type),
            FromStatus = r.FromStatus is null ? null : CheckCodes.ParseItemStatus(r.FromStatus),
            ToStatus = r.ToStatus is null ? null : CheckCodes.ParseItemStatus(r.ToStatus),
            OccurredAt = IsoTimestamp.Parse(r.OccurredAt)
        }).ToList();
    }

    private static Task InsertEventAsync(
        IDbConnection connection, IDbTransaction transaction, CheckEvent checkEvent, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO check_events (id, check_id, app_id, type, from_status, to_status, occurred_at)
            VALUES (@Id, @CheckId, @AppId, @Type, @FromStatus, @ToStatus, @OccurredAt)
            """,
            new
            {
                checkEvent.Id,
                checkEvent.CheckId,
                checkEvent.AppId,
                Type = checkEvent.Type.ToCode(),
                FromStatus = checkEvent.FromStatus?.ToCode(),
                ToStatus = checkEvent.ToStatus?.ToCode(),
                OccurredAt = IsoTimestamp.Format(checkEvent.OccurredAt)
            },
            transaction, cancellationToken: cancellationToken));

    private static object ToCheckParameters(Check check) => new
    {
        check.Id,
        check.Title,
        check.Description,
        check.Category,
        check.Author,
        StartDate = FormatDate(check.StartDate),
        EndDate = FormatDate(check.EndDate),
        Status = check.Status.ToCode(),
        CreatedAt = IsoTimestamp.Format(check.CreatedAt),
        ClosedAt = check.ClosedAt is null ? null : IsoTimestamp.Format(check.ClosedAt.Value)
    };

    private static object ToItemParameters(CheckItem item) => new
    {
        item.CheckId,
        item.AppId,
        Status = item.Status.ToCode(),
        item.Notes,
        UpdatedAt = IsoTimestamp.Format(item.UpdatedAt)
    };

    // Datas de calendário (sem hora) como TEXT yyyy-MM-dd — portável e ordenável.
    private static string? FormatDate(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateOnly? ParseDate(string? value) =>
        value is null ? null : DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed record CheckRow(
        string Id, string Title, string? Description, string? Category, string? Author,
        string? StartDate, string? EndDate, string Status, string CreatedAt, string? ClosedAt)
    {
        public Check ToCheck() => new()
        {
            Id = Id,
            Title = Title,
            Description = Description,
            Category = Category,
            Author = Author,
            StartDate = ParseDate(StartDate),
            EndDate = ParseDate(EndDate),
            Status = CheckCodes.ParseStatus(Status),
            CreatedAt = IsoTimestamp.Parse(CreatedAt),
            ClosedAt = ClosedAt is null ? null : IsoTimestamp.Parse(ClosedAt)
        };
    }

    private sealed record CheckItemRow(string CheckId, string AppId, string Status, string? Notes, string UpdatedAt);

    private sealed record CheckItemViewRow(
        string AppId, string AppName, string SquadId, string SquadName, string? AppStatus,
        string Status, string? Notes, string UpdatedAt);

    private sealed record AppCheckEntryRow(
        string CheckId, string Title, string CheckStatus, string? ClosedAt, string ItemStatus, string? Notes);

    private sealed record CandidateRow(string AppId, string Name, string SquadId, string SquadName, string? Status);

    private sealed record CheckEventRow(
        string Id, string CheckId, string? AppId, string Type, string? FromStatus, string? ToStatus, string OccurredAt);
}
