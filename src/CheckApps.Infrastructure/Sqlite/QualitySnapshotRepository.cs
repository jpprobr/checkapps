using Dapper;
using CheckApps.Application.Persistence;
using CheckApps.Domain;

namespace CheckApps.Infrastructure.Sqlite;

public class QualitySnapshotRepository(SqliteConnectionFactory connectionFactory) : IQualitySnapshotRepository
{
    private const string SelectColumns =
        """
        SELECT id AS Id, app_id AS AppId, sonar_project_id AS SonarProjectId,
               collected_at AS CollectedAt, source AS Source,
               coverage AS Coverage, bugs AS Bugs, vulnerabilities AS Vulnerabilities,
               code_smells AS CodeSmells, duplications AS Duplications,
               reliability AS Reliability, maintainability AS Maintainability, security AS Security,
               quality_level AS QualityLevel, assessment_rules_version AS AssessmentRulesVersion
        FROM quality_snapshots
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
            WHERE app_id = @appId
              AND (@from IS NULL OR collected_at >= @from)
              AND (@to IS NULL OR collected_at <= @to)
            ORDER BY collected_at, sonar_project_id
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
            WHERE app_id = @appId
            ORDER BY collected_at DESC, sonar_project_id DESC
            LIMIT 1
            """,
            new { appId }, cancellationToken: cancellationToken));

        return row?.ToSnapshot();
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
