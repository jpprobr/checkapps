using CheckApps.Application.Persistence;
using CheckApps.Domain.Analysis;
using Dapper;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.Infrastructure.Sqlite;

/// <summary>
/// SQL explícito e portável (ADR-009). A "análise corrente" de um repositório é a subconsulta
/// <see cref="CurrentIdFor"/>: a mais recente que não falhou. Avisos são guardados como texto, um por linha.
/// </summary>
public class AnalysisRepository(SqliteConnectionFactory connectionFactory) : IAnalysisRepository
{
    private const string FactColumns =
        """
        d.id AS Id, d.analysis_id AS AnalysisId, d.ecosystem AS Ecosystem, d.kind AS Kind, d.name AS Name,
        d.version AS Version, d.project_path AS ProjectPath, d.source_file AS SourceFile, d.origin AS Origin,
        d.confidence AS Confidence, d.confidence_note AS ConfidenceNote, d.scanner_id AS ScannerId,
        d.scanner_version AS ScannerVersion
        """;

    private const string IntegrationColumns =
        """
        i.id AS Id, i.analysis_id AS AnalysisId, i.mechanism AS Mechanism, i.scheme AS Scheme, i.host AS Host,
        i.port AS Port, i.base_path AS BasePath, i.reference AS Reference, i.config_key AS ConfigKey,
        i.environment AS Environment, i.environment_kind AS EnvironmentKind, i.state AS State,
        i.project_path AS ProjectPath, i.source_file AS SourceFile, i.origin AS Origin, i.confidence AS Confidence,
        i.confidence_note AS ConfidenceNote, i.scanner_id AS ScannerId, i.scanner_version AS ScannerVersion
        """;

    private const string AnalysisColumns =
        """
        an.id AS Id, an.repository_id AS RepositoryId, an.analyzed_at AS AnalyzedAt, an.commit_sha AS CommitSha,
        an.status AS Status, an.warnings AS Warnings, an.source AS Source
        """;

    /// <summary>Subconsulta (portável, com LIMIT) do id da análise corrente do repositório identificado por <paramref name="repositoryIdExpression"/>.</summary>
    private static string CurrentIdFor(string repositoryIdExpression) =>
        $"""
        (SELECT x.id FROM repository_analyses x
         WHERE x.repository_id = {repositoryIdExpression} AND x.status <> '{AnalysisStatuses.Failed}'
         ORDER BY x.analyzed_at DESC, x.id DESC LIMIT 1)
        """;

    public async Task AddAsync(
        RepositoryAnalysis analysis, IReadOnlyList<RepositoryDependency> facts,
        IReadOnlyList<RepositoryIntegration>? integrations = null, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO repository_analyses (id, repository_id, analyzed_at, commit_sha, status, warnings, source)
            VALUES (@Id, @RepositoryId, @AnalyzedAt, @CommitSha, @Status, @Warnings, @Source)
            """,
            new
            {
                analysis.Id,
                analysis.RepositoryId,
                AnalyzedAt = IsoTimestamp.Format(analysis.AnalyzedAt),
                analysis.CommitSha,
                analysis.Status,
                Warnings = analysis.Warnings.Count == 0 ? null : string.Join('\n', analysis.Warnings),
                analysis.Source
            },
            transaction, cancellationToken: cancellationToken));

        foreach (var fact in facts)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO repository_dependencies
                    (id, analysis_id, ecosystem, kind, name, version, project_path, source_file, origin,
                     confidence, confidence_note, scanner_id, scanner_version)
                VALUES
                    (@Id, @AnalysisId, @Ecosystem, @Kind, @Name, @Version, @ProjectPath, @SourceFile, @Origin,
                     @Confidence, @ConfidenceNote, @ScannerId, @ScannerVersion)
                """,
                fact, transaction, cancellationToken: cancellationToken));
        }

        foreach (var integration in integrations ?? [])
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO repository_integrations
                    (id, analysis_id, mechanism, scheme, host, port, base_path, reference, config_key, environment,
                     environment_kind, state, project_path, source_file, origin, confidence, confidence_note,
                     scanner_id, scanner_version)
                VALUES
                    (@Id, @AnalysisId, @Mechanism, @Scheme, @Host, @Port, @BasePath, @Reference, @ConfigKey, @Environment,
                     @EnvironmentKind, @State, @ProjectPath, @SourceFile, @Origin, @Confidence, @ConfidenceNote,
                     @ScannerId, @ScannerVersion)
                """,
                integration, transaction, cancellationToken: cancellationToken));
        }

        transaction.Commit();
    }

    public async Task<RepositoryAnalysis?> GetCurrentAsync(string repositoryId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var row = await connection.QuerySingleOrDefaultAsync<AnalysisRow>(new CommandDefinition(
            $"SELECT {AnalysisColumns} FROM repository_analyses an WHERE an.id = {CurrentIdFor("@repositoryId")}",
            new { repositoryId }, cancellationToken: cancellationToken));
        return row?.ToEntity();
    }

    /// <summary>Os repositórios do app, cada um com a análise corrente e a falha mais nova que ela (se houver).</summary>
    private static async Task<IReadOnlyList<(Repository Repository, RepositoryAnalysis? Current, RepositoryAnalysis? LastFailure)>> LoadCurrentAsync(
        System.Data.IDbConnection connection, string appId, CancellationToken cancellationToken)
    {
        var repositories = (await connection.QueryAsync<Repository>(new CommandDefinition(
            """
            SELECT id AS Id, app_id AS AppId, name AS Name, provider AS Provider, url AS Url,
                   external_id AS ExternalId, status AS Status,
                   sigla AS Sigla, sigla_app AS SiglaApp, role AS Role, source AS Source
            FROM repositories WHERE app_id = @appId ORDER BY name
            """,
            new { appId }, cancellationToken: cancellationToken))).ToList();

        if (repositories.Count == 0)
        {
            return [];
        }

        var ids = repositories.Select(r => r.Id).ToArray();

        var current = (await connection.QueryAsync<AnalysisRow>(new CommandDefinition(
            $"SELECT {AnalysisColumns} FROM repository_analyses an WHERE an.repository_id IN @ids AND an.id = {CurrentIdFor("an.repository_id")}",
            new { ids }, cancellationToken: cancellationToken)))
            .Select(r => r.ToEntity())
            .ToDictionary(a => a.RepositoryId);

        var failures = (await connection.QueryAsync<AnalysisRow>(new CommandDefinition(
            $"""
            SELECT {AnalysisColumns} FROM repository_analyses an
            WHERE an.repository_id IN @ids AND an.status = '{AnalysisStatuses.Failed}'
              AND an.id = (SELECT x.id FROM repository_analyses x
                           WHERE x.repository_id = an.repository_id AND x.status = '{AnalysisStatuses.Failed}'
                           ORDER BY x.analyzed_at DESC, x.id DESC LIMIT 1)
            """,
            new { ids }, cancellationToken: cancellationToken)))
            .Select(r => r.ToEntity())
            .ToDictionary(a => a.RepositoryId);

        return repositories.Select(repository =>
        {
            current.TryGetValue(repository.Id, out var analysis);
            failures.TryGetValue(repository.Id, out var failure);

            // Só interessa a falha que veio depois da análise corrente (ou quando não há corrente).
            var lastFailure = failure is not null && (analysis is null || failure.AnalyzedAt > analysis.AnalyzedAt) ? failure : null;
            return (repository, analysis, lastFailure);
        }).ToList();
    }

    public async Task<IReadOnlyList<RepositoryAnalysisView>> GetCurrentByAppAsync(
        string appId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var loaded = await LoadCurrentAsync(connection, appId, cancellationToken);

        var analysisIds = loaded.Where(l => l.Current is not null).Select(l => l.Current!.Id).ToArray();
        var facts = analysisIds.Length == 0
            ? []
            : (await connection.QueryAsync<RepositoryDependency>(new CommandDefinition(
                $"SELECT {FactColumns} FROM repository_dependencies d WHERE d.analysis_id IN @analysisIds ORDER BY d.kind, d.project_path, d.name",
                new { analysisIds }, cancellationToken: cancellationToken))).ToList();

        return loaded.Select(l => new RepositoryAnalysisView(
            l.Repository, l.Current, l.LastFailure,
            l.Current is null ? [] : facts.Where(f => f.AnalysisId == l.Current.Id).ToList())).ToList();
    }

    public async Task<IReadOnlyList<RepositoryIntegrationsView>> GetCurrentIntegrationsByAppAsync(
        string appId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var loaded = await LoadCurrentAsync(connection, appId, cancellationToken);

        var analysisIds = loaded.Where(l => l.Current is not null).Select(l => l.Current!.Id).ToArray();
        var integrations = analysisIds.Length == 0
            ? []
            : (await connection.QueryAsync<IntegrationRow>(new CommandDefinition(
                $"SELECT {IntegrationColumns} FROM repository_integrations i WHERE i.analysis_id IN @analysisIds ORDER BY i.reference, i.config_key, i.source_file",
                new { analysisIds }, cancellationToken: cancellationToken))).Select(r => r.ToEntity()).ToList();

        return loaded.Select(l => new RepositoryIntegrationsView(
            l.Repository, l.Current, l.LastFailure,
            l.Current is null ? [] : integrations.Where(i => i.AnalysisId == l.Current.Id).ToList())).ToList();
    }

    public async Task<IReadOnlyList<IntegrationUsageRow>> ListIntegrationRowsAsync(
        string? hostContains, string? squadId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var rows = await connection.QueryAsync<IntegrationUsageDbRow>(new CommandDefinition(
            $"""
            SELECT {IntegrationColumns},
                   r.id AS RepositoryId, r.name AS RepositoryName, r.status AS RepositoryStatus,
                   a.id AS AppId, a.name AS AppName, s.id AS SquadId, s.name AS SquadName,
                   an.analyzed_at AS AnalyzedAt
            FROM repository_integrations i
            JOIN repository_analyses an ON an.id = i.analysis_id
            JOIN repositories r ON r.id = an.repository_id
            JOIN apps a ON a.id = r.app_id
            JOIN squads s ON s.id = a.squad_id
            WHERE i.host IS NOT NULL
              AND (@pattern IS NULL OR lower(i.host) LIKE @pattern ESCAPE '\')
              AND (@squadId IS NULL OR s.id = @squadId)
              AND an.id = {CurrentIdFor("r.id")}
            ORDER BY lower(i.host), a.name, r.name, i.reference, i.config_key, i.source_file
            """,
            new { pattern = LikePattern(hostContains), squadId }, cancellationToken: cancellationToken));
        return rows.Select(r => r.ToRow()).ToList();
    }

    /// <summary>Trecho de host → padrão LIKE (contém), com os curingas digitados escapados. Portável (ESCAPE explícito).</summary>
    private static string? LikePattern(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var escaped = text.Trim().ToLowerInvariant().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        return $"%{escaped}%";
    }

    public async Task<IReadOnlyList<PackageUsageRow>> SearchPackagesAsync(
        string? name, string? squadId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var rows = await connection.QueryAsync<PackageRow>(new CommandDefinition(
            $"""
            SELECT {FactColumns},
                   r.id AS RepositoryId, r.name AS RepositoryName, r.status AS RepositoryStatus,
                   a.id AS AppId, a.name AS AppName, s.id AS SquadId, s.name AS SquadName,
                   an.analyzed_at AS AnalyzedAt
            FROM repository_dependencies d
            JOIN repository_analyses an ON an.id = d.analysis_id
            JOIN repositories r ON r.id = an.repository_id
            JOIN apps a ON a.id = r.app_id
            JOIN squads s ON s.id = a.squad_id
            WHERE d.kind = '{FactKinds.Package}'
              AND (@name IS NULL OR lower(d.name) = @name)
              AND (@squadId IS NULL OR s.id = @squadId)
              AND an.id = {CurrentIdFor("r.id")}
            ORDER BY a.name, r.name, d.project_path, d.name
            """,
            new { name = name?.Trim().ToLowerInvariant(), squadId }, cancellationToken: cancellationToken));
        return rows.Select(r => r.ToRow()).ToList();
    }

    public async Task<IReadOnlyList<FrameworkUsageRow>> ListFrameworkUsageAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var rows = await connection.QueryAsync<FrameworkUsageRow>(new CommandDefinition(
            $"""
            SELECT a.id AS AppId, a.name AS AppName, s.id AS SquadId, s.name AS SquadName,
                   f.name AS Framework, r.name AS RepositoryName
            FROM apps a
            JOIN squads s ON s.id = a.squad_id
            LEFT JOIN repositories r ON r.app_id = a.id AND lower(coalesce(r.status, '')) = 'active'
            LEFT JOIN repository_dependencies f
                   ON f.kind = '{FactKinds.Framework}' AND f.analysis_id = {CurrentIdFor("r.id")}
            WHERE a.status IS NULL OR lower(a.status) <> 'inactive'
            ORDER BY s.name, a.name, f.name, r.name
            """,
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<AnalysisCoverage> GetCoverageAsync(CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var unanalyzed = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"""
            SELECT COUNT(*) FROM repositories r
            WHERE NOT EXISTS (SELECT 1 FROM repository_analyses x
                              WHERE x.repository_id = r.id AND x.status <> '{AnalysisStatuses.Failed}')
            """,
            cancellationToken: cancellationToken));
        var latest = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            $"SELECT MAX(analyzed_at) FROM repository_analyses WHERE status <> '{AnalysisStatuses.Failed}'",
            cancellationToken: cancellationToken));
        return new AnalysisCoverage(unanalyzed, latest is null ? null : IsoTimestamp.Parse(latest));
    }

    private sealed class AnalysisRow
    {
        public string Id { get; set; } = "";
        public string RepositoryId { get; set; } = "";
        public string AnalyzedAt { get; set; } = "";
        public string? CommitSha { get; set; }
        public string Status { get; set; } = "";
        public string? Warnings { get; set; }
        public string Source { get; set; } = "";

        public RepositoryAnalysis ToEntity() => new()
        {
            Id = Id,
            RepositoryId = RepositoryId,
            AnalyzedAt = IsoTimestamp.Parse(AnalyzedAt),
            CommitSha = CommitSha,
            Status = Status,
            Warnings = string.IsNullOrEmpty(Warnings) ? [] : Warnings.Split('\n'),
            Source = Source
        };
    }

    private class IntegrationRow
    {
        public string Id { get; set; } = "";
        public string AnalysisId { get; set; } = "";
        public string Mechanism { get; set; } = "";
        public string? Scheme { get; set; }
        public string? Host { get; set; }
        public int? Port { get; set; }
        public string? BasePath { get; set; }
        public string Reference { get; set; } = "";
        public string ConfigKey { get; set; } = "";
        public string? Environment { get; set; }
        public string? EnvironmentKind { get; set; }
        public string State { get; set; } = "";
        public string ProjectPath { get; set; } = "";
        public string SourceFile { get; set; } = "";
        public string Origin { get; set; } = "";
        public string Confidence { get; set; } = "";
        public string? ConfidenceNote { get; set; }
        public string ScannerId { get; set; } = "";
        public string ScannerVersion { get; set; } = "";

        public RepositoryIntegration ToEntity() => new()
        {
            Id = Id,
            AnalysisId = AnalysisId,
            Mechanism = Mechanism,
            Scheme = Scheme,
            Host = Host,
            Port = Port,
            BasePath = BasePath,
            Reference = Reference,
            ConfigKey = ConfigKey,
            Environment = Environment,
            EnvironmentKind = EnvironmentKind,
            State = State,
            ProjectPath = ProjectPath,
            SourceFile = SourceFile,
            Origin = Origin,
            Confidence = Confidence,
            ConfidenceNote = ConfidenceNote,
            ScannerId = ScannerId,
            ScannerVersion = ScannerVersion
        };
    }

    private sealed class IntegrationUsageDbRow : IntegrationRow
    {
        public string RepositoryId { get; set; } = "";
        public string RepositoryName { get; set; } = "";
        public string? RepositoryStatus { get; set; }
        public string AppId { get; set; } = "";
        public string AppName { get; set; } = "";
        public string? SquadId { get; set; }
        public string? SquadName { get; set; }
        public string AnalyzedAt { get; set; } = "";

        public IntegrationUsageRow ToRow() =>
            new(ToEntity(), RepositoryId, RepositoryName, RepositoryStatus, AppId, AppName, SquadId, SquadName, IsoTimestamp.Parse(AnalyzedAt));
    }

    private sealed class PackageRow
    {
        public string Id { get; set; } = "";
        public string AnalysisId { get; set; } = "";
        public string Ecosystem { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Version { get; set; }
        public string ProjectPath { get; set; } = "";
        public string SourceFile { get; set; } = "";
        public string Origin { get; set; } = "";
        public string Confidence { get; set; } = "";
        public string? ConfidenceNote { get; set; }
        public string ScannerId { get; set; } = "";
        public string ScannerVersion { get; set; } = "";
        public string RepositoryId { get; set; } = "";
        public string RepositoryName { get; set; } = "";
        public string? RepositoryStatus { get; set; }
        public string AppId { get; set; } = "";
        public string AppName { get; set; } = "";
        public string? SquadId { get; set; }
        public string? SquadName { get; set; }
        public string AnalyzedAt { get; set; } = "";

        public PackageUsageRow ToRow() => new(
            new RepositoryDependency
            {
                Id = Id,
                AnalysisId = AnalysisId,
                Ecosystem = Ecosystem,
                Kind = Kind,
                Name = Name,
                Version = Version,
                ProjectPath = ProjectPath,
                SourceFile = SourceFile,
                Origin = Origin,
                Confidence = Confidence,
                ConfidenceNote = ConfidenceNote,
                ScannerId = ScannerId,
                ScannerVersion = ScannerVersion
            },
            RepositoryId, RepositoryName, RepositoryStatus, AppId, AppName, SquadId, SquadName, IsoTimestamp.Parse(AnalyzedAt));
    }
}
