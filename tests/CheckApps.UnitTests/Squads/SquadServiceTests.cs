using CheckApps.Application.Apps;
using CheckApps.Application.Persistence;
using CheckApps.Application.Squads;
using CheckApps.Domain;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.UnitTests.Squads;

public class SquadServiceTests
{
    private static readonly DateTimeOffset Jun = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Jul = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Aug = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetQualityEvolution_ClassifiesEachAppTrend_ImprovedWorsenedStableNew()
    {
        var apps = new FakeAppRepository();
        var snapshots = new FakeQualitySnapshotRepository();

        apps.Add(new App { Id = "app-improving", SquadId = "sq-1", Name = "improving" });
        snapshots.Add("app-improving", Jul, QualityLevel.B);
        snapshots.Add("app-improving", Aug, QualityLevel.A);

        apps.Add(new App { Id = "app-worsening", SquadId = "sq-1", Name = "worsening" });
        snapshots.Add("app-worsening", Jul, QualityLevel.B);
        snapshots.Add("app-worsening", Aug, QualityLevel.C);

        apps.Add(new App { Id = "app-stable", SquadId = "sq-1", Name = "stable" });
        snapshots.Add("app-stable", Jul, QualityLevel.A);
        snapshots.Add("app-stable", Aug, QualityLevel.A);

        apps.Add(new App { Id = "app-new", SquadId = "sq-1", Name = "new" });
        snapshots.Add("app-new", Aug, QualityLevel.B);

        var service = new SquadService(new FakeSquadRepository(), apps, snapshots);

        var evolution = await service.GetQualityEvolutionAsync("sq-1");

        Assert.Equal(QualityTrendDirection.Improved, Trend(evolution, "app-improving"));
        Assert.Equal(QualityTrendDirection.Worsened, Trend(evolution, "app-worsening"));
        Assert.Equal(QualityTrendDirection.Stable, Trend(evolution, "app-stable"));
        Assert.Equal(QualityTrendDirection.New, Trend(evolution, "app-new"));
    }

    [Fact]
    public async Task GetQualityEvolution_GroupsLevelCounts_ByCollectionDate()
    {
        var apps = new FakeAppRepository();
        var snapshots = new FakeQualitySnapshotRepository();

        apps.Add(new App { Id = "app-1", SquadId = "sq-1", Name = "a1" });
        apps.Add(new App { Id = "app-2", SquadId = "sq-1", Name = "a2" });
        snapshots.Add("app-1", Jun, QualityLevel.A);
        snapshots.Add("app-2", Jun, QualityLevel.B);

        var service = new SquadService(new FakeSquadRepository(), apps, snapshots);

        var evolution = await service.GetQualityEvolutionAsync("sq-1");

        var point = Assert.Single(evolution.Points);
        Assert.Equal(Jun, point.Period);
        Assert.Equal(1, point.LevelCounts[QualityLevel.A]);
        Assert.Equal(1, point.LevelCounts[QualityLevel.B]);
    }

    private static QualityTrendDirection Trend(SquadQualityEvolution evolution, string appId) =>
        evolution.AppTrends.Single(t => t.AppId == appId).Direction;

    private class FakeSquadRepository : ISquadRepository
    {
        public Task<IReadOnlyList<Squad>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Squad>>([]);
        public Task<Squad?> GetAsync(string squadId, CancellationToken ct = default) =>
            Task.FromResult<Squad?>(null);
        public Task SaveAsync(Squad squad, CancellationToken ct = default) => Task.CompletedTask;
    }

    private class FakeAppRepository : IAppRepository
    {
        private readonly List<App> _apps = [];

        public void Add(App app) => _apps.Add(app);

        public Task<App?> GetAsync(string appId, CancellationToken ct = default) =>
            Task.FromResult(_apps.FirstOrDefault(a => a.Id == appId));

        public Task<IReadOnlyList<App>> ListBySquadAsync(string squadId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<App>>(_apps.Where(a => a.SquadId == squadId).ToList());

        public Task<IReadOnlyList<App>> ListByQualityLevelAsync(QualityLevel level, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<App>>(_apps.Where(a => a.CurrentQualityLevel == level).ToList());

        public Task<IReadOnlyList<Repository>> GetRepositoriesAsync(string appId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Repository>>([]);

        public Task<IReadOnlyList<SonarProject>> GetSonarProjectsAsync(string appId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SonarProject>>([]);

        public Task SaveAsync(App app, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddRepositoryAsync(Repository repository, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddSonarProjectAsync(SonarProject sonarProject, CancellationToken ct = default) => Task.CompletedTask;
    }

    private class FakeQualitySnapshotRepository : IQualitySnapshotRepository
    {
        private readonly List<QualitySnapshot> _snapshots = [];

        public void Add(string appId, DateTimeOffset collectedAt, QualityLevel level) => _snapshots.Add(new QualitySnapshot
        {
            Id = $"{appId}-{collectedAt:yyyyMM}",
            AppId = appId,
            SonarProjectId = "sonar-1",
            CollectedAt = collectedAt,
            Source = "TEST",
            Metrics = new QualityMetrics
            {
                Coverage = 0,
                Bugs = 0,
                Vulnerabilities = 0,
                CodeSmells = 0,
                Duplications = 0,
                Reliability = level.ToString(),
                Maintainability = level.ToString(),
                Security = level.ToString()
            },
            QualityLevel = level,
            AssessmentRulesVersion = "v1"
        });

        public Task AddAsync(QualitySnapshot snapshot, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<QualitySnapshot>> GetHistoryAsync(
            string appId, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<QualitySnapshot>>(
                _snapshots.Where(s => s.AppId == appId).OrderBy(s => s.CollectedAt).ToList());

        public Task<QualitySnapshot?> GetLatestAsync(string appId, CancellationToken ct = default) =>
            Task.FromResult(_snapshots.Where(s => s.AppId == appId).OrderByDescending(s => s.CollectedAt).FirstOrDefault());
    }
}
