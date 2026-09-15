using CheckApps.Domain;
using CheckApps.Domain.QualityAssessment;

namespace CheckApps.UnitTests.QualityAssessment;

public class QualityAssessmentEngineV1Tests
{
    private readonly QualityAssessmentEngineV1 _sut = new();

    private static QualityMetrics Metrics(
        double coverage = 80,
        int bugs = 0,
        int vulnerabilities = 0,
        int codeSmells = 0,
        double duplications = 5,
        string reliability = "A",
        string maintainability = "A",
        string security = "A") => new()
        {
            Coverage = coverage,
            Bugs = bugs,
            Vulnerabilities = vulnerabilities,
            CodeSmells = codeSmells,
            Duplications = duplications,
            Reliability = reliability,
            Maintainability = maintainability,
            Security = security
        };

    [Fact]
    public void RulesVersion_IsV1()
    {
        Assert.Equal("v1", _sut.RulesVersion);
    }

    [Fact]
    public void AllRatingsGood_HighCoverage_LowDuplication_ResultsInA()
    {
        var level = _sut.Assess(Metrics());

        Assert.Equal(QualityLevel.A, level);
    }

    [Theory]
    [InlineData("A", "A", "C", QualityLevel.C)]
    [InlineData("B", "A", "A", QualityLevel.B)]
    [InlineData("E", "A", "A", QualityLevel.E)]
    public void BaseLevel_IsTheWorstOfTheThreeRatings(
        string reliability, string maintainability, string security, QualityLevel expected)
    {
        var level = _sut.Assess(Metrics(reliability: reliability, maintainability: maintainability, security: security));

        Assert.Equal(expected, level);
    }

    [Fact]
    public void LowCoverage_DowngradesOneLevel()
    {
        var level = _sut.Assess(Metrics(coverage: 40));

        Assert.Equal(QualityLevel.B, level);
    }

    [Fact]
    public void HighDuplication_DowngradesOneLevel()
    {
        var level = _sut.Assess(Metrics(duplications: 25));

        Assert.Equal(QualityLevel.B, level);
    }

    [Fact]
    public void LowCoverageAndHighDuplication_DowngradesTwoLevels()
    {
        var level = _sut.Assess(Metrics(coverage: 40, duplications: 25));

        Assert.Equal(QualityLevel.C, level);
    }

    [Fact]
    public void Downgrade_NeverGoesBeyondE()
    {
        var level = _sut.Assess(Metrics(
            reliability: "E", maintainability: "E", security: "E",
            coverage: 10, duplications: 90));

        Assert.Equal(QualityLevel.E, level);
    }
}
