using CheckApps.Application.Persistence;
using CheckApps.Domain;
using CheckApps.Domain.QualityAssessment;

namespace CheckApps.Application.QualitySnapshots;

/// <summary>
/// Único ponto de entrada para registrar um QualitySnapshot: garante que o
/// qualityLevel seja sempre calculado pelo motor de assessment vigente antes da
/// escrita (ver seção 8 do plano — desvios desse caminho quebram o estado
/// desnormalizado do App).
/// </summary>
public class QualitySnapshotService(IQualitySnapshotRepository snapshots, IQualityAssessmentEngine assessmentEngine)
{
    public async Task<QualitySnapshot> RegisterAsync(
        string appId,
        string sonarProjectId,
        QualityMetrics metrics,
        DateTimeOffset collectedAt,
        string source,
        CancellationToken cancellationToken = default)
    {
        var snapshot = new QualitySnapshot
        {
            Id = Guid.NewGuid().ToString("n"),
            AppId = appId,
            SonarProjectId = sonarProjectId,
            CollectedAt = collectedAt,
            Source = source,
            Metrics = metrics,
            QualityLevel = assessmentEngine.Assess(metrics),
            AssessmentRulesVersion = assessmentEngine.RulesVersion
        };

        await snapshots.AddAsync(snapshot, cancellationToken);
        return snapshot;
    }
}
