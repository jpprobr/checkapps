namespace CheckApps.Domain.QualityAssessment;

public interface IQualityAssessmentEngine
{
    string RulesVersion { get; }

    QualityLevel Assess(QualityMetrics metrics);
}
