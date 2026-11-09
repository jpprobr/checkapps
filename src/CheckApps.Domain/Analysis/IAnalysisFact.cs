namespace CheckApps.Domain.Analysis;

/// <summary>Um fato pronto para gravar, de qualquer tipo, pertencente a uma <see cref="RepositoryAnalysis"/> (spec 021).</summary>
public interface IAnalysisFact
{
    string AnalysisId { get; }
}
