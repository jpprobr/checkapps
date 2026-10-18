using Dapper;
using CheckApps.Application.Discovery;
using CheckApps.Application.Persistence;
using CheckApps.Domain.Discovery;

namespace CheckApps.Infrastructure.Sqlite;

public class DiscoveredRepositoryRepository(SqliteConnectionFactory connectionFactory) : IDiscoveredRepositoryRepository
{
    private const string SelectColumns =
        """
        id AS Id, provider AS Provider, external_id AS ExternalId, name AS Name, url AS Url,
        sigla AS Sigla, sigla_app AS SiglaApp, archived AS Archived, pushed_at AS PushedAt,
        status AS Status, first_seen_at AS FirstSeenAt, last_seen_at AS LastSeenAt, source AS Source
        """;

    public async Task<UpsertOutcome> UpsertAsync(DiscoveredRepository repository, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        using var transaction = connection.BeginTransaction();

        var existing = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM discovered_repositories WHERE provider = @Provider AND external_id = @ExternalId",
            new { repository.Provider, repository.ExternalId }, transaction, cancellationToken: cancellationToken));

        if (existing is null)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO discovered_repositories
                    (id, provider, external_id, name, url, sigla, sigla_app, archived, pushed_at,
                     status, first_seen_at, last_seen_at, source)
                VALUES
                    (@Id, @Provider, @ExternalId, @Name, @Url, @Sigla, @SiglaApp, @Archived, @PushedAt,
                     @Status, @FirstSeenAt, @LastSeenAt, @Source)
                """,
                ToParameters(repository), transaction, cancellationToken: cancellationToken));
            transaction.Commit();
            return UpsertOutcome.New;
        }

        var unchanged =
            existing.Name == repository.Name
            && existing.Url == repository.Url
            && existing.Sigla == repository.Sigla
            && existing.SiglaApp == repository.SiglaApp
            && (existing.Archived != 0) == repository.Archived
            && existing.PushedAt == FormatNullable(repository.PushedAt);

        // Só os dados observados e last_seen_at; status, first_seen_at e source ficam como estão.
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE discovered_repositories SET
                name = @Name, url = @Url, sigla = @Sigla, sigla_app = @SiglaApp,
                archived = @Archived, pushed_at = @PushedAt, last_seen_at = @LastSeenAt
            WHERE provider = @Provider AND external_id = @ExternalId
            """,
            ToParameters(repository), transaction, cancellationToken: cancellationToken));
        transaction.Commit();

        return unchanged ? UpsertOutcome.Unchanged : UpsertOutcome.Updated;
    }

    public async Task<DiscoveredRepository?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            $"SELECT {SelectColumns} FROM discovered_repositories WHERE id = @id",
            new { id }, cancellationToken: cancellationToken));
        return row?.ToEntity();
    }

    public async Task<IReadOnlyList<DiscoveredRepository>> ListAsync(
        TriageStatus? status, string? sigla, CatalogScope scope = CatalogScope.Pending,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(
            $"""
            SELECT {SelectColumns} FROM discovered_repositories d
            WHERE (@status IS NULL OR d.status = @status)
              AND (@sigla IS NULL OR d.sigla = @sigla)
              AND (@scope = 'any'
                   OR (@scope = 'pending' AND NOT EXISTS (
                           SELECT 1 FROM repositories r
                           WHERE r.provider = d.provider AND r.external_id = d.external_id))
                   OR (@scope = 'cataloged' AND EXISTS (
                           SELECT 1 FROM repositories r
                           WHERE r.provider = d.provider AND r.external_id = d.external_id)))
            ORDER BY d.sigla, d.name
            """,
            new
            {
                status = status?.ToCode(),
                sigla = sigla?.Trim().ToLowerInvariant(),
                scope = scope.ToString().ToLowerInvariant()
            },
            cancellationToken: cancellationToken));
        return rows.Select(r => r.ToEntity()).ToList();
    }

    public async Task SetStatusAsync(string id, TriageStatus status, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE discovered_repositories SET status = @status WHERE id = @id",
            new { id, status = status.ToCode() }, cancellationToken: cancellationToken));
    }

    public async Task<bool> IsInCatalogAsync(string provider, string externalId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM repositories WHERE provider = @provider AND external_id = @externalId)",
            new { provider, externalId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyDictionary<string, CatalogApp>> GetCatalogAppsAsync(
        string provider, IReadOnlyCollection<string> externalIds, CancellationToken cancellationToken = default)
    {
        if (externalIds.Count == 0)
        {
            return new Dictionary<string, CatalogApp>();
        }

        using var connection = connectionFactory.Create();
        var rows = await connection.QueryAsync<(string ExternalId, string AppId, string AppName, string? SquadId, string? SquadName)>(new CommandDefinition(
            """
            SELECT r.external_id AS ExternalId, a.id AS AppId, a.name AS AppName,
                   s.id AS SquadId, s.name AS SquadName
            FROM repositories r
            JOIN apps a ON a.id = r.app_id
            LEFT JOIN squads s ON s.id = a.squad_id
            WHERE r.provider = @provider AND r.external_id IN @externalIds
            ORDER BY a.id
            """,
            new { provider, externalIds }, cancellationToken: cancellationToken));

        // (provider, external_id) não é único em repositories; se houver mais de um app, vale o primeiro por id.
        return rows
            .GroupBy(r => r.ExternalId)
            .ToDictionary(g => g.Key, g => new CatalogApp(g.First().AppId, g.First().AppName, g.First().SquadId, g.First().SquadName));
    }

    public async Task<bool> IsSiglaAppInUseAsync(string siglaApp, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM repositories WHERE sigla_app = @siglaApp)",
            new { siglaApp }, cancellationToken: cancellationToken));
    }

    private static object ToParameters(DiscoveredRepository r) => new
    {
        r.Id,
        r.Provider,
        r.ExternalId,
        r.Name,
        r.Url,
        r.Sigla,
        r.SiglaApp,
        Archived = r.Archived ? 1 : 0,
        PushedAt = FormatNullable(r.PushedAt),
        Status = r.Status.ToCode(),
        FirstSeenAt = IsoTimestamp.Format(r.FirstSeenAt),
        LastSeenAt = IsoTimestamp.Format(r.LastSeenAt),
        r.Source
    };

    private static string? FormatNullable(DateTimeOffset? value) => value is null ? null : IsoTimestamp.Format(value.Value);

    private sealed record Row(
        string Id, string Provider, string ExternalId, string Name, string? Url, string Sigla, string? SiglaApp,
        long Archived, string? PushedAt, string Status, string FirstSeenAt, string LastSeenAt, string Source)
    {
        public DiscoveredRepository ToEntity() => new()
        {
            Id = Id,
            Provider = Provider,
            ExternalId = ExternalId,
            Name = Name,
            Url = Url,
            Sigla = Sigla,
            SiglaApp = SiglaApp,
            Archived = Archived != 0,
            PushedAt = PushedAt is null ? null : IsoTimestamp.Parse(PushedAt),
            Status = TriageCodes.ParseStatus(Status),
            FirstSeenAt = IsoTimestamp.Parse(FirstSeenAt),
            LastSeenAt = IsoTimestamp.Parse(LastSeenAt),
            Source = Source
        };
    }
}
