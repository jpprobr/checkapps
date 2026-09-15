namespace CheckApps.Domain.QualityAssessment;

/// <summary>
/// v1: nível base é o pior entre reliability/maintainability/security;
/// cobertura baixa ou duplicação alta rebaixam um nível.
/// </summary>
public class QualityAssessmentEngineV1 : IQualityAssessmentEngine
{
    private const double MinCoveragePercent = 60;
    private const double MaxDuplicationsPercent = 20;

    public string RulesVersion => "v1";

    public QualityLevel Assess(QualityMetrics metrics)
    {
        var baseLevel = Worst(
            Enum.Parse<QualityLevel>(metrics.Reliability),
            Enum.Parse<QualityLevel>(metrics.Maintainability),
            Enum.Parse<QualityLevel>(metrics.Security));

        var level = baseLevel;

        if (metrics.Coverage < MinCoveragePercent)
        {
            level = Downgrade(level);
        }

        if (metrics.Duplications > MaxDuplicationsPercent)
        {
            level = Downgrade(level);
        }

        return level;
    }

    private static QualityLevel Worst(params QualityLevel[] levels) => levels.Max();

    private static QualityLevel Downgrade(QualityLevel level) =>
        level == QualityLevel.E ? level : level + 1;
}
