using CheckApps.Domain;
using CheckApps.Domain.QualityAssessment;
using CheckApps.Infrastructure.Seeding;

namespace CheckApps.UnitTests.Seeding;

public class DemoDataFactoryTests
{
    private readonly DemoDataSet _data = new DemoDataFactory(new QualityAssessmentEngineV1()).Build();

    [Fact]
    public void Produces_TwoSquads()
    {
        Assert.Equal(2, _data.Squads.Count);
    }

    [Fact]
    public void EveryApp_Has_ThreeMonthlySnapshots_OrderedByTime()
    {
        foreach (var app in _data.Apps)
        {
            var appSnapshots = _data.QualitySnapshots
                .Where(s => s.AppId == app.Id)
                .OrderBy(s => s.CollectedAt)
                .ToList();

            Assert.Equal(3, appSnapshots.Count);
            Assert.True(appSnapshots[0].CollectedAt < appSnapshots[1].CollectedAt);
            Assert.True(appSnapshots[1].CollectedAt < appSnapshots[2].CollectedAt);
        }
    }

    [Fact]
    public void App_CurrentState_MatchesLatestSnapshot()
    {
        foreach (var app in _data.Apps)
        {
            var latest = _data.QualitySnapshots
                .Where(s => s.AppId == app.Id)
                .OrderByDescending(s => s.CollectedAt)
                .First();

            Assert.Equal(latest.QualityLevel, app.CurrentQualityLevel);
            Assert.Equal(latest.Id, app.LastSnapshotId);
        }
    }

    [Fact]
    public void ContainsAnImprovingApp()
    {
        var levels = LevelsOverTime("app-1");
        Assert.Equal([QualityLevel.B, QualityLevel.B, QualityLevel.A], levels);
    }

    [Fact]
    public void ContainsAWorseningApp()
    {
        var levels = LevelsOverTime("app-2");
        Assert.Equal([QualityLevel.B, QualityLevel.C, QualityLevel.C], levels);
    }

    [Fact]
    public void ContainsAStableApp()
    {
        var levels = LevelsOverTime("app-3");
        Assert.Equal([QualityLevel.A, QualityLevel.A, QualityLevel.A], levels);
    }

    [Fact]
    public void ContainsAnAppWithMultipleRepositories()
    {
        Assert.Equal(2, _data.Repositories.Count(r => r.AppId == "app-2"));
    }

    [Fact]
    public void ContainsAnAppWithMultipleSonarProjects()
    {
        Assert.Equal(2, _data.SonarProjects.Count(p => p.AppId == "app-5"));
    }

    private List<QualityLevel> LevelsOverTime(string appId) =>
        _data.QualitySnapshots
            .Where(s => s.AppId == appId)
            .OrderBy(s => s.CollectedAt)
            .Select(s => s.QualityLevel)
            .ToList();
}
