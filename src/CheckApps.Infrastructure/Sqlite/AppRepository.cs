using System.Data;
using Dapper;
using CheckApps.Application.Persistence;
using CheckApps.Domain;
using Microsoft.Data.Sqlite;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.Infrastructure.Sqlite;

public class AppRepository(SqliteConnectionFactory connectionFactory) : IAppRepository
{
    /// <summary>
    /// O estado de qualidade atual é derivado do snapshot mais recente, não desnormalizado
    /// (ADR-009 revoga a cláusula 3 do ADR-004). O desempate por sonar_project_id espelha a
    /// antiga ordenação da SK (SNAPSHOT#{iso}#{sonarProjectId}) quando dois projetos Sonar
    /// coletam no mesmo instante. A subconsulta correlacionada é portável para Postgres.
    /// </summary>
    private const string SelectAppWithCurrentQuality =
        """
        SELECT a.id AS Id, a.squad_id AS SquadId, a.name AS Name, a.description AS Description,
               a.criticality AS Criticality, a.status AS Status, a.external_id AS ExternalId,
               s.id AS SnapshotId, s.collected_at AS CollectedAt, s.quality_level AS QualityLevel,
               s.assessment_rules_version AS AssessmentRulesVersion,
               s.coverage AS Coverage, s.bugs AS Bugs, s.vulnerabilities AS Vulnerabilities,
               s.code_smells AS CodeSmells, s.duplications AS Duplications,
               s.reliability AS Reliability, s.maintainability AS Maintainability, s.security AS Security
        FROM apps a
        LEFT JOIN quality_snapshots s ON s.id = (
            SELECT id FROM quality_snapshots
            WHERE app_id = a.id
            ORDER BY collected_at DESC, sonar_project_id DESC
            LIMIT 1)
        """;

    public async Task<App?> GetAsync(string appId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var row = await connection.QuerySingleOrDefaultAsync<AppRow>(new CommandDefinition(
            $"{SelectAppWithCurrentQuality} WHERE a.id = @appId",
            new { appId }, cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        var tags = await LoadTagsAsync(connection, [row.Id], cancellationToken);
        return row.ToApp(tags.GetValueOrDefault(row.Id, []));
    }

    public Task<IReadOnlyList<App>> ListBySquadAsync(string squadId, CancellationToken cancellationToken = default) =>
        QueryAppsAsync($"{SelectAppWithCurrentQuality} WHERE a.squad_id = @squadId ORDER BY a.id",
            new { squadId }, cancellationToken);

    public Task<IReadOnlyList<App>> ListByQualityLevelAsync(QualityLevel level, CancellationToken cancellationToken = default) =>
        QueryAppsAsync($"{SelectAppWithCurrentQuality} WHERE s.quality_level = @level ORDER BY a.id",
            new { level = level.ToString() }, cancellationToken);

    public async Task<IReadOnlyList<Repository>> GetRepositoriesAsync(string appId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var repositories = await connection.QueryAsync<Repository>(new CommandDefinition(
            """
            SELECT id AS Id, app_id AS AppId, name AS Name, provider AS Provider,
                   url AS Url, external_id AS ExternalId, status AS Status,
                   sigla AS Sigla, sigla_app AS SiglaApp, role AS Role, source AS Source
            FROM repositories WHERE app_id = @appId ORDER BY id
            """,
            new { appId }, cancellationToken: cancellationToken));
        return repositories.ToList();
    }

    public async Task<IReadOnlyList<SonarProject>> GetSonarProjectsAsync(string appId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var projects = await connection.QueryAsync<SonarProject>(new CommandDefinition(
            """
            SELECT id AS Id, app_id AS AppId, project_key AS ProjectKey, name AS Name,
                   url AS Url, external_id AS ExternalId, status AS Status, source AS Source
            FROM sonar_projects WHERE app_id = @appId ORDER BY id
            """,
            new { appId }, cancellationToken: cancellationToken));
        return projects.ToList();
    }

    /// <summary>
    /// Grava apenas os dados próprios do App. Os campos de qualidade são projeção de leitura
    /// e nunca são escritos — por isso construir um App novo aqui já não apaga nada (era a
    /// armadilha do SaveAsync no DynamoDB, que fazia PutItem do item inteiro).
    /// </summary>
    public async Task SaveAsync(App app, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO apps (id, squad_id, name, description, criticality, status, external_id)
            VALUES (@Id, @SquadId, @Name, @Description, @Criticality, @Status, @ExternalId)
            ON CONFLICT (id) DO UPDATE SET
                squad_id = excluded.squad_id,
                name = excluded.name,
                description = excluded.description,
                criticality = excluded.criticality,
                status = excluded.status,
                external_id = excluded.external_id
            """,
            ToAppParameters(app), transaction, cancellationToken: cancellationToken));

        await ReplaceTagsAsync(connection, transaction, app, cancellationToken);
        transaction.Commit();
    }

    public async Task<bool> TryCreateAsync(App app, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        using var transaction = connection.BeginTransaction();

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO apps (id, squad_id, name, description, criticality, status, external_id)
                VALUES (@Id, @SquadId, @Name, @Description, @Criticality, @Status, @ExternalId)
                """,
                ToAppParameters(app), transaction, cancellationToken: cancellationToken));
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode is SqliteErrorCodes.ConstraintPrimaryKey or SqliteErrorCodes.ConstraintUnique)
        {
            transaction.Rollback();
            return false;
        }

        await ReplaceTagsAsync(connection, transaction, app, cancellationToken);
        transaction.Commit();
        return true;
    }

    /// <summary>
    /// Apaga o App; repositórios, projetos Sonar, links e snapshots saem por cascata do schema.
    /// Como no DynamoDB, é um primitivo sem guarda: a regra "não remover app com histórico"
    /// é do AppService.
    /// </summary>
    public async Task DeleteAllAsync(string appId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM apps WHERE id = @appId", new { appId }, cancellationToken: cancellationToken));
    }

    public async Task AddRepositoryAsync(Repository repository, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO repositories (id, app_id, name, provider, url, external_id, status, sigla, sigla_app, role, source)
            VALUES (@Id, @AppId, @Name, @Provider, @Url, @ExternalId, @Status, @Sigla, @SiglaApp, @Role, @Source)
            ON CONFLICT (id) DO UPDATE SET
                app_id = excluded.app_id, name = excluded.name, provider = excluded.provider,
                url = excluded.url, external_id = excluded.external_id, status = excluded.status,
                sigla = excluded.sigla, sigla_app = excluded.sigla_app, role = excluded.role,
                source = excluded.source
            """,
            repository, cancellationToken: cancellationToken));
    }

    public async Task<Repository?> GetRepositoryAsync(
        string appId, string repositoryId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        return await connection.QuerySingleOrDefaultAsync<Repository>(new CommandDefinition(
            """
            SELECT id AS Id, app_id AS AppId, name AS Name, provider AS Provider, url AS Url,
                   external_id AS ExternalId, status AS Status,
                   sigla AS Sigla, sigla_app AS SiglaApp, role AS Role, source AS Source
            FROM repositories WHERE app_id = @appId AND id = @repositoryId
            """,
            new { appId, repositoryId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteRepositoryAsync(string appId, string repositoryId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM repositories WHERE app_id = @appId AND id = @repositoryId",
            new { appId, repositoryId }, cancellationToken: cancellationToken));
    }

    public async Task<bool> IsSiglaAppInUseAsync(
        string siglaApp, string? exceptRepositoryId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM repositories WHERE sigla_app = @siglaApp AND id <> @exceptRepositoryId)",
            new { siglaApp, exceptRepositoryId = exceptRepositoryId ?? string.Empty },
            cancellationToken: cancellationToken));
    }

    public async Task AddSonarProjectAsync(SonarProject sonarProject, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO sonar_projects (id, app_id, project_key, name, url, external_id, status, source)
            VALUES (@Id, @AppId, @ProjectKey, @Name, @Url, @ExternalId, @Status, @Source)
            ON CONFLICT (id) DO UPDATE SET
                app_id = excluded.app_id, project_key = excluded.project_key, name = excluded.name,
                url = excluded.url, external_id = excluded.external_id, status = excluded.status,
                source = excluded.source
            """,
            sonarProject, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AppLink>> GetLinksAsync(string appId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var links = await connection.QueryAsync<AppLink>(new CommandDefinition(
            """
            SELECT id AS Id, app_id AS AppId, description AS Description, uri AS Uri
            FROM app_links WHERE app_id = @appId ORDER BY id
            """,
            new { appId }, cancellationToken: cancellationToken));
        return links.ToList();
    }

    public async Task SaveLinkAsync(AppLink link, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO app_links (id, app_id, description, uri)
            VALUES (@Id, @AppId, @Description, @Uri)
            ON CONFLICT (id) DO UPDATE SET
                description = excluded.description, uri = excluded.uri
            """,
            link, cancellationToken: cancellationToken));
    }

    public async Task DeleteLinkAsync(string appId, string linkId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM app_links WHERE app_id = @appId AND id = @linkId",
            new { appId, linkId }, cancellationToken: cancellationToken));
    }

    private async Task<IReadOnlyList<App>> QueryAppsAsync(
        string sql, object parameters, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.Create();
        var rows = (await connection.QueryAsync<AppRow>(new CommandDefinition(
            sql, parameters, cancellationToken: cancellationToken))).ToList();

        if (rows.Count == 0)
        {
            return [];
        }

        var tags = await LoadTagsAsync(connection, rows.Select(r => r.Id).ToList(), cancellationToken);
        return rows.Select(r => r.ToApp(tags.GetValueOrDefault(r.Id, []))).ToList();
    }

    /// <summary>
    /// Tags vêm em consulta separada, não por GROUP_CONCAT: a função de agregação de strings
    /// difere entre SQLite e Postgres, e portabilidade é requisito (FR-006).
    /// </summary>
    private static async Task<Dictionary<string, List<string>>> LoadTagsAsync(
        IDbConnection connection, IReadOnlyList<string> appIds, CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<(string AppId, string Tag)>(new CommandDefinition(
            "SELECT app_id, tag FROM app_tags WHERE app_id IN @appIds ORDER BY tag",
            new { appIds }, cancellationToken: cancellationToken));

        return rows
            .GroupBy(r => r.AppId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Tag).ToList());
    }

    private static async Task ReplaceTagsAsync(
        IDbConnection connection, IDbTransaction transaction, App app, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM app_tags WHERE app_id = @appId",
            new { appId = app.Id }, transaction, cancellationToken: cancellationToken));

        if (app.Tags.Count == 0)
        {
            return;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO app_tags (app_id, tag) VALUES (@appId, @tag)",
            app.Tags.Distinct().Select(tag => new { appId = app.Id, tag }).ToList(),
            transaction, cancellationToken: cancellationToken));
    }

    private static object ToAppParameters(App app) => new
    {
        app.Id,
        app.SquadId,
        app.Name,
        app.Description,
        app.Criticality,
        app.Status,
        app.ExternalId
    };

    private sealed record AppRow(
        string Id, string SquadId, string Name, string? Description, string? Criticality,
        string? Status, string? ExternalId,
        string? SnapshotId, string? CollectedAt, string? QualityLevel, string? AssessmentRulesVersion,
        double? Coverage, long? Bugs, long? Vulnerabilities, long? CodeSmells, double? Duplications,
        string? Reliability, string? Maintainability, string? Security)
    {
        public App ToApp(IReadOnlyCollection<string> tags) => new()
        {
            Id = Id,
            SquadId = SquadId,
            Name = Name,
            Description = Description,
            Criticality = Criticality,
            Tags = tags,
            Status = Status,
            ExternalId = ExternalId,
            CurrentQualityLevel = QualityLevel is null ? null : Enum.Parse<QualityLevel>(QualityLevel),
            CurrentMetrics = Coverage is null ? null : new QualityMetrics
            {
                Coverage = Coverage.Value,
                Bugs = (int)Bugs!.Value,
                Vulnerabilities = (int)Vulnerabilities!.Value,
                CodeSmells = (int)CodeSmells!.Value,
                Duplications = Duplications!.Value,
                Reliability = Reliability!,
                Maintainability = Maintainability!,
                Security = Security!
            },
            LastSnapshotAt = CollectedAt is null ? null : IsoTimestamp.Parse(CollectedAt),
            LastSnapshotId = SnapshotId,
            AssessmentRulesVersion = AssessmentRulesVersion
        };
    }
}
