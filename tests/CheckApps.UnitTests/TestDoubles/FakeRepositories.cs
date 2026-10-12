using CheckApps.Application.Checks;
using CheckApps.Application.Persistence;
using CheckApps.Domain;
using CheckApps.Domain.Checks;
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

    /// <summary>O fake não conhece squads; devolve tudo, e os testes montam só os apps da squad sob teste.</summary>
    public Task<IReadOnlyList<QualitySnapshot>> GetHistoryBySquadAsync(
        string squadId, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<QualitySnapshot>>(
            _snapshots.OrderBy(s => s.AppId).ThenBy(s => s.CollectedAt).ToList());
}

/// <summary>
/// Itens e histórico em memória. A visão de item usa o próprio appId como nome e uma
/// squad fictícia — os testes de serviço não dependem do join, que é coberto pela integração.
/// </summary>
public class FakeCheckRepository : ICheckRepository
{
    private readonly Dictionary<string, Check> _checks = [];
    private readonly List<CheckItem> _items = [];
    private readonly List<CheckEvent> _events = [];

    public IReadOnlyList<CheckEvent> Events => _events;

    public Task<bool> TryCreateAsync(Check check, CancellationToken ct = default) =>
        Task.FromResult(_checks.TryAdd(check.Id, check));

    public Task SaveAsync(Check check, CancellationToken ct = default)
    {
        _checks[check.Id] = check;
        return Task.CompletedTask;
    }

    public Task<Check?> GetAsync(string checkId, CancellationToken ct = default) =>
        Task.FromResult(_checks.GetValueOrDefault(checkId));

    public Task<IReadOnlyList<CheckSummary>> ListAsync(bool includeClosed, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CheckSummary>>(_checks.Values
            .Where(c => includeClosed || c.Status == CheckStatus.Open)
            .Select(c => new CheckSummary(c, CheckProgress.From(_items.Where(i => i.CheckId == c.Id).Select(i => i.Status))))
            .ToList());

    public Task<IReadOnlyList<CheckItemView>> GetItemsAsync(string checkId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CheckItemView>>(_items
            .Where(i => i.CheckId == checkId)
            .Select(i => new CheckItemView(i.AppId, i.AppId, "sq", "Squad", "active", i.Status, i.Notes, i.UpdatedAt))
            .ToList());

    public Task<CheckItem?> GetItemAsync(string checkId, string appId, CancellationToken ct = default) =>
        Task.FromResult(_items.SingleOrDefault(i => i.CheckId == checkId && i.AppId == appId));

    public Task<IReadOnlyList<AppCheckEntry>> ListByAppAsync(string appId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AppCheckEntry>>(_items
            .Where(i => i.AppId == appId)
            .Select(i =>
            {
                var c = _checks[i.CheckId];
                return new AppCheckEntry(c.Id, c.Title, c.Status, c.ClosedAt, i.Status, i.Notes);
            })
            .ToList());

    public Task<IReadOnlyList<CheckCandidateApp>> ListCandidateAppsAsync(string checkId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CheckCandidateApp>>([]);

    public Task<bool> AddItemAsync(CheckItem item, CheckEvent added, CancellationToken ct = default)
    {
        if (_items.Any(i => i.CheckId == item.CheckId && i.AppId == item.AppId))
        {
            return Task.FromResult(false);
        }

        _items.Add(item);
        _events.Add(added);
        return Task.FromResult(true);
    }

    public Task RemoveItemAsync(string checkId, string appId, CheckEvent removed, CancellationToken ct = default)
    {
        _items.RemoveAll(i => i.CheckId == checkId && i.AppId == appId);
        _events.Add(removed);
        return Task.CompletedTask;
    }

    public Task UpdateItemAsync(CheckItem item, CheckEvent? statusChanged, CancellationToken ct = default)
    {
        _items.RemoveAll(i => i.CheckId == item.CheckId && i.AppId == item.AppId);
        _items.Add(item);
        if (statusChanged is not null)
        {
            _events.Add(statusChanged);
        }
        return Task.CompletedTask;
    }

    public Task SetStatusAsync(Check check, CheckEvent changed, CancellationToken ct = default)
    {
        _checks[check.Id] = check;
        _events.Add(changed);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string checkId, CancellationToken ct = default)
    {
        _checks.Remove(checkId);
        _events.RemoveAll(e => e.CheckId == checkId);
        return Task.CompletedTask;
    }

    public Task<bool> IsAppInAnyCheckAsync(string appId, CancellationToken ct = default) =>
        Task.FromResult(_items.Any(i => i.AppId == appId));

    public Task<IReadOnlyList<CheckEvent>> GetHistoryAsync(string checkId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CheckEvent>>(_events
            .Where(e => e.CheckId == checkId)
            .OrderByDescending(e => e.OccurredAt)
            .ToList());
}

/// <summary>Relógio controlável: cada chamada a <see cref="Advance"/> move o tempo.</summary>
public class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}
