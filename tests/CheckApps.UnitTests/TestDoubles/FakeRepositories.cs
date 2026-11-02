using CheckApps.Application.Checks;
using CheckApps.Application.Discovery;
using CheckApps.Application.Persistence;
using CheckApps.Domain;
using CheckApps.Domain.Checks;
using CheckApps.Domain.Discovery;
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
    private readonly List<Repository> _repositories = [];

    public IReadOnlyList<Repository> AllRepositories => _repositories;

    /// <summary>Busca síncrona, para os outros doubles resolverem o nome do app.</summary>
    public App? Find(string appId) => _apps.GetValueOrDefault(appId);

    public void Add(App app) => _apps[app.Id] = app;

    public Task<App?> GetAsync(string appId, CancellationToken ct = default) =>
        Task.FromResult(_apps.GetValueOrDefault(appId));

    public Task<IReadOnlyList<App>> ListBySquadAsync(string squadId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<App>>(_apps.Values.Where(a => a.SquadId == squadId).ToList());

    public Task<IReadOnlyList<App>> ListAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<App>>(_apps.Values.ToList());

    public Task<IReadOnlyList<App>> ListByQualityLevelAsync(QualityLevel level, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<App>>(_apps.Values.Where(a => a.CurrentQualityLevel == level).ToList());

    public Task<IReadOnlyList<Repository>> GetRepositoriesAsync(string appId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Repository>>(_repositories.Where(r => r.AppId == appId).ToList());

    public Task<IReadOnlyList<SonarProject>> GetSonarProjectsAsync(string appId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SonarProject>>([]);

    public Task<IReadOnlyList<CatalogRepository>> ListRepositoriesBySiglaAsync(string sigla, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CatalogRepository>>(_repositories
            .Where(r => string.Equals(r.Sigla, sigla.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(r => new CatalogRepository(r, r.AppId, _apps.GetValueOrDefault(r.AppId)?.Name ?? r.AppId))
            .ToList());

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
        _repositories.RemoveAll(r => r.AppId == appId);
        foreach (var linkId in _links.Values.Where(l => l.AppId == appId).Select(l => l.Id).ToList())
        {
            _links.Remove(linkId);
        }
        return Task.CompletedTask;
    }

    public Task AddRepositoryAsync(Repository repository, CancellationToken ct = default)
    {
        _repositories.RemoveAll(r => r.Id == repository.Id);
        _repositories.Add(repository);
        return Task.CompletedTask;
    }

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

    public Task<IReadOnlyList<CatalogRepository>> ListCatalogOnlyRepositoriesAsync(string? sigla, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CatalogRepository>>(_repositories
            .Where(r => sigla is null || string.Equals(r.Sigla, sigla, StringComparison.OrdinalIgnoreCase))
            .Select(r => new CatalogRepository(r, r.AppId, _apps.GetValueOrDefault(r.AppId)?.Name ?? r.AppId))
            .ToList());

    public Task<Repository?> GetRepositoryAsync(string appId, string repositoryId, CancellationToken ct = default) =>
        Task.FromResult(_repositories.FirstOrDefault(r => r.AppId == appId && r.Id == repositoryId));

    public Task DeleteRepositoryAsync(string appId, string repositoryId, CancellationToken ct = default)
    {
        _repositories.RemoveAll(r => r.AppId == appId && r.Id == repositoryId);
        return Task.CompletedTask;
    }

    public Task<bool> IsSiglaAppInUseAsync(string siglaApp, string? exceptRepositoryId, CancellationToken ct = default) =>
        Task.FromResult(_repositories.Any(r =>
            string.Equals(r.SiglaApp, siglaApp, StringComparison.OrdinalIgnoreCase) && r.Id != exceptRepositoryId));
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

/// <summary>
/// Caixa de entrada em memória. "No catálogo" e "sigla-app em uso" olham o <see cref="FakeAppRepository"/>,
/// como o banco real olha a tabela repositories.
/// </summary>
public class FakeDiscoveredRepositoryRepository(FakeAppRepository apps) : IDiscoveredRepositoryRepository
{
    private readonly Dictionary<string, DiscoveredRepository> _items = [];

    public int Count => _items.Count;

    public Task<UpsertOutcome> UpsertAsync(DiscoveredRepository repository, CancellationToken ct = default)
    {
        if (!_items.TryGetValue(repository.Id, out var existing))
        {
            _items[repository.Id] = Clone(repository);
            return Task.FromResult(UpsertOutcome.New);
        }

        var unchanged = existing.SameObservation(repository);
        existing.Name = repository.Name;
        existing.Url = repository.Url;
        existing.Sigla = repository.Sigla;
        existing.SiglaApp = repository.SiglaApp;
        existing.Archived = repository.Archived;
        existing.PushedAt = repository.PushedAt;
        existing.LastSeenAt = repository.LastSeenAt;
        return Task.FromResult(unchanged ? UpsertOutcome.Unchanged : UpsertOutcome.Updated);
    }

    public Task<DiscoveredRepository?> GetAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(_items.GetValueOrDefault(id));

    public Task<IReadOnlyList<DiscoveredRepository>> ListAsync(
        TriageStatus? status, string? sigla, CatalogScope scope = CatalogScope.Pending, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<DiscoveredRepository>>(_items.Values
            .Where(d => status is null || d.Status == status)
            .Where(d => sigla is null || d.Sigla == sigla.ToLowerInvariant())
            .Where(d => scope == CatalogScope.Any
                || (scope == CatalogScope.Pending) != InCatalog(d))
            .OrderBy(d => d.Sigla).ThenBy(d => d.Name)
            .ToList());

    public Task SetStatusAsync(string id, TriageStatus status, CancellationToken ct = default)
    {
        _items[id].Status = status;
        return Task.CompletedTask;
    }

    public Task<bool> IsInCatalogAsync(DiscoveredRepository repository, CancellationToken ct = default) =>
        Task.FromResult(InCatalog(repository));

    public Task<IReadOnlyDictionary<string, CatalogApp>> GetCatalogAppsAsync(
        IReadOnlyCollection<DiscoveredRepository> repositories, CancellationToken ct = default)
    {
        var result = new Dictionary<string, CatalogApp>();
        foreach (var d in repositories)
        {
            var match = apps.AllRepositories.OrderBy(r => r.AppId).FirstOrDefault(r => Matches(r, d));
            if (match is not null)
            {
                result[d.Id] = new CatalogApp(match.AppId, apps.Find(match.AppId)?.Name ?? match.AppId);
            }
        }

        return Task.FromResult<IReadOnlyDictionary<string, CatalogApp>>(result);
    }

    public Task<bool> IsSiglaAppInUseAsync(string siglaApp, CancellationToken ct = default) =>
        Task.FromResult(apps.AllRepositories.Any(r => string.Equals(r.SiglaApp, siglaApp, StringComparison.OrdinalIgnoreCase)));

    private bool InCatalog(DiscoveredRepository d) => apps.AllRepositories.Any(r => Matches(r, d));

    private static bool Matches(Repository r, DiscoveredRepository d) =>
        (r.Provider == d.Provider && r.ExternalId == d.ExternalId) || RepositoryUrl.Same(r.Url, d.Url);

    private static DiscoveredRepository Clone(DiscoveredRepository r) => new()
    {
        Id = r.Id,
        Provider = r.Provider,
        ExternalId = r.ExternalId,
        Name = r.Name,
        Url = r.Url,
        Sigla = r.Sigla,
        SiglaApp = r.SiglaApp,
        Archived = r.Archived,
        PushedAt = r.PushedAt,
        Status = r.Status,
        FirstSeenAt = r.FirstSeenAt,
        LastSeenAt = r.LastSeenAt,
        Source = r.Source
    };
}

/// <summary>Fonte com lista fixa por sigla; conta as consultas para provar o que NÃO foi consultado.</summary>
public class StubDiscoverySource : IRepositoryDiscoverySource
{
    private readonly Dictionary<string, List<DiscoveredRepositoryInfo>> _bySigla = [];

    public string Provider => "stub";

    public string? ConfigurationProblem { get; set; }

    public List<string> Calls { get; } = [];

    public int? FailWithTotalFor { get; set; }

    public string? FailOnSigla { get; set; }

    public void Add(string sigla, DiscoveredRepositoryInfo info)
    {
        if (!_bySigla.TryGetValue(sigla, out var list))
        {
            _bySigla[sigla] = list = [];
        }
        list.Add(info);
    }

    public Task<IReadOnlyList<DiscoveredRepositoryInfo>> DiscoverAsync(
        string sigla, string namePrefix, int maxResults, CancellationToken ct = default)
    {
        Calls.Add(sigla);
        if (sigla == FailOnSigla)
        {
            throw new ScanLimitExceededException(sigla, FailWithTotalFor ?? maxResults + 1, maxResults);
        }

        return Task.FromResult<IReadOnlyList<DiscoveredRepositoryInfo>>(_bySigla.GetValueOrDefault(sigla) ?? []);
    }
}
