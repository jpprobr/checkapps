using CheckApps.Application.Persistence;
using CheckApps.Domain;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.UnitTests.TestDoubles;

public class FakeSquadRepository : ISquadRepository
{
    private readonly Dictionary<string, Squad> _squads = [];

    public Task<IReadOnlyList<Squad>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Squad>>(_squads.Values.ToList());

    public Task<Squad?> GetAsync(string squadId, CancellationToken ct = default) =>
        Task.FromResult(_squads.GetValueOrDefault(squadId));

    public Task SaveAsync(Squad squad, CancellationToken ct = default)
    {
        _squads[squad.Id] = squad;
        return Task.CompletedTask;
    }

    public Task<bool> TryCreateAsync(Squad squad, CancellationToken ct = default)
    {
        if (!_squads.TryAdd(squad.Id, squad))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public Task DeleteAsync(string squadId, CancellationToken ct = default)
    {
        _squads.Remove(squadId);
        return Task.CompletedTask;
    }
}

public class FakeAppRepository : IAppRepository
{
    private readonly Dictionary<string, App> _apps = [];
    private readonly Dictionary<string, AppLink> _links = [];

    public void Add(App app) => _apps[app.Id] = app;

    public Task<App?> GetAsync(string appId, CancellationToken ct = default) =>
        Task.FromResult(_apps.GetValueOrDefault(appId));

    public Task<IReadOnlyList<App>> ListBySquadAsync(string squadId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<App>>(_apps.Values.Where(a => a.SquadId == squadId).ToList());

    public Task<IReadOnlyList<App>> ListByQualityLevelAsync(QualityLevel level, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<App>>(_apps.Values.Where(a => a.CurrentQualityLevel == level).ToList());

    public Task<IReadOnlyList<Repository>> GetRepositoriesAsync(string appId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Repository>>([]);

    public Task<IReadOnlyList<SonarProject>> GetSonarProjectsAsync(string appId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SonarProject>>([]);

    public Task SaveAsync(App app, CancellationToken ct = default)
    {
        _apps[app.Id] = app;
        return Task.CompletedTask;
    }

    public Task<bool> TryCreateAsync(App app, CancellationToken ct = default)
    {
        if (!_apps.TryAdd(app.Id, app))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public Task DeleteAllAsync(string appId, CancellationToken ct = default)
    {
        _apps.Remove(appId);
        foreach (var linkId in _links.Values.Where(l => l.AppId == appId).Select(l => l.Id).ToList())
        {
            _links.Remove(linkId);
        }
        return Task.CompletedTask;
    }

    public Task AddRepositoryAsync(Repository repository, CancellationToken ct = default) => Task.CompletedTask;

    public Task AddSonarProjectAsync(SonarProject sonarProject, CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<AppLink>> GetLinksAsync(string appId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AppLink>>(_links.Values.Where(l => l.AppId == appId).ToList());

    public Task SaveLinkAsync(AppLink link, CancellationToken ct = default)
    {
        _links[link.Id] = link;
        return Task.CompletedTask;
    }

    public Task DeleteLinkAsync(string appId, string linkId, CancellationToken ct = default)
    {
        _links.Remove(linkId);
        return Task.CompletedTask;
    }
}

public class FakeQualitySnapshotRepository : IQualitySnapshotRepository
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

    public Task AddAsync(QualitySnapshot snapshot, CancellationToken ct = default)
    {
        _snapshots.Add(snapshot);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<QualitySnapshot>> GetHistoryAsync(
        string appId, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<QualitySnapshot>>(
            _snapshots.Where(s => s.AppId == appId).OrderBy(s => s.CollectedAt).ToList());

    public Task<QualitySnapshot?> GetLatestAsync(string appId, CancellationToken ct = default) =>
        Task.FromResult(_snapshots.Where(s => s.AppId == appId).OrderByDescending(s => s.CollectedAt).FirstOrDefault());
}
