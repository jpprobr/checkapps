using Dapper;
using CheckApps.Application.Persistence;
using CheckApps.Domain;

namespace CheckApps.Infrastructure.Sqlite;

public class QualitySnapshotRepository(SqliteConnectionFactory connectionFactory) : IQualitySnapshotRepository
{
    private const string SelectColumns =
        """
        SELECT s.id AS Id, s.app_id AS AppId, s.sonar_project_id AS SonarProjectId,
               s.collected_at AS CollectedAt, s.source AS Source,
               s.coverage AS Coverage, s.bugs AS Bugs, s.vulnerabilities AS Vulnerabilities,
               s.code_smells AS CodeSmells, s.duplications AS Duplications,
               s.reliability AS Reliability, s.maintainability AS Maintainability, s.security AS Security,
               s.quality_level AS QualityLevel, s.assessment_rules_version AS AssessmentRulesVersion
        FROM quality_snapshots s
        """;

    /// <summary>
    /// Insert simples. A transação condicional do DynamoDB existia só para não deixar um
    /// backfill fora de ordem regredir o estado desnormalizado do App; sem desnormalização,
    /// o "mais recente" é derivado por consulta e backfill fica correto por construção.
    /// </summary>
    public async Task AddAsync(QualitySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO quality_snapshots (
                id, app_id, sonar_project_id, collected_at, source,
                coverage, bugs, vulnerabilities, code_smells, duplications,
                reliability, maintainability, security, quality_level, assessment_rules_version)
            VALUES (
                @Id, @AppId, @SonarProjectId, @CollectedAt, @Source,
                @Coverage, @Bugs, @Vulnerabilities, @CodeSmells, @Duplications,
                @Reliability, @Maintainability, @Security, @QualityLevel, @AssessmentRulesVersion)
            ON CONFLICT (id) DO NOTHING
            """,
            new
            {
                snapshot.Id,
                snapshot.AppId,
                snapshot.SonarProjectId,
                CollectedAt = IsoTimestamp.Format(snapshot.CollectedAt),
                snapshot.Source,
                snapshot.Metrics.Coverage,
                snapshot.Metrics.Bugs,
                snapshot.Metrics.Vulnerabilities,
                snapshot.Metrics.CodeSmells,
                snapshot.Metrics.Duplications,
                snapshot.Metrics.Reliability,
                snapshot.Metrics.Maintainability,
                snapshot.Metrics.Security,
                QualityLevel = snapshot.QualityLevel.ToString(),
                snapshot.AssessmentRulesVersion
            },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<QualitySnapshot>> GetHistoryAsync(
        string appId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        // from e to são inclusivos, como eram os limites de SK no DynamoDB.
        var sql = $"""
            {SelectColumns}
            WHERE s.app_id = @appId
              AND (@from IS NULL OR s.collected_at >= @from)
              AND (@to IS NULL OR s.collected_at <= @to)
            ORDER BY s.collected_at, s.sonar_project_id
            """;

        using var connection = connectionFactory.Create();
        var rows = await connection.QueryAsync<SnapshotRow>(new CommandDefinition(
            sql,
            new
            {
                appId,
                from = from is { } f ? IsoTimestamp.Format(f) : null,
                to = to is { } t ? IsoTimestamp.Format(t) : null
            },
            cancellationToken: cancellationToken));

        return rows.Select(r => r.ToSnapshot()).ToList();
    }

    public async Task<QualitySnapshot?> GetLatestAsync(string appId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.Create();
        var row = await connection.QuerySingleOrDefaultAsync<SnapshotRow>(new CommandDefinition(
            $"""
            {SelectColumns}
            WHERE s.app_id = @appId
            ORDER BY s.collected_at DESC, s.sonar_project_id DESC
            LIMIT 1
            """,
            new { appId }, cancellationToken: cancellationToken));

        return row?.ToSnapshot();
    }

    public async Task<IReadOnlyList<QualitySnapshot>> GetHistoryBySquadAsync(
        string squadId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        var sql = $"""
            {SelectColumns}
            INNER JOIN apps a ON a.id = s.app_id
            WHERE a.squad_id = @squadId
              AND (@from IS NULL OR s.collected_at >= @from)
              AND (@to IS NULL OR s.collected_at <= @to)
            ORDER BY s.app_id, s.collected_at, s.sonar_project_id
            """;

        using var connection = connectionFactory.Create();
        var rows = await connection.QueryAsync<SnapshotRow>(new CommandDefinition(
            sql,
            new
            {
                squadId,
                from = from is { } f ? IsoTimestamp.Format(f) : null,
                to = to is { } t ? IsoTimestamp.Format(t) : null
            },
            cancellationToken: cancellationToken));

        return rows.Select(r => r.ToSnapshot()).ToList();
    }

    private sealed record SnapshotRow(
        string Id, string AppId, string SonarProjectId, string CollectedAt, string Source,
        double Coverage, long Bugs, long Vulnerabilities, long CodeSmells, double Duplications,
        string Reliability, string Maintainability, string Security,
        string QualityLevel, string AssessmentRulesVersion)
    {
        public QualitySnapshot ToSnapshot() => new()
        {
            Id = Id,
            AppId = AppId,
            SonarProjectId = SonarProjectId,
            CollectedAt = IsoTimestamp.Parse(CollectedAt),
            Source = Source,
            Metrics = new QualityMetrics
            {
                Coverage = Coverage,
                Bugs = (int)Bugs,
                Vulnerabilities = (int)Vulnerabilities,
                CodeSmells = (int)CodeSmells,
                Duplications = Duplications,
                Reliability = Reliability,
                Maintainability = Maintainability,
                Security = Security
            },
            QualityLevel = Enum.Parse<QualityLevel>(QualityLevel),
            AssessmentRulesVersion = AssessmentRulesVersion
        };
    }
}
