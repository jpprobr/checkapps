using CheckApps.Application.Persistence;
using CheckApps.Domain;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.Application.Apps;

public class AppService(IAppRepository apps, IQualitySnapshotRepository snapshots)
{
    public Task<App?> GetAppAsync(string appId, CancellationToken cancellationToken = default) =>
        apps.GetAsync(appId, cancellationToken);

    public Task<IReadOnlyList<Repository>> ListRepositoriesAsync(string appId, CancellationToken cancellationToken = default) =>
        apps.GetRepositoriesAsync(appId, cancellationToken);

    public Task<IReadOnlyList<SonarProject>> ListSonarProjectsAsync(string appId, CancellationToken cancellationToken = default) =>
        apps.GetSonarProjectsAsync(appId, cancellationToken);

    public Task<IReadOnlyList<App>> ListByQualityLevelAsync(QualityLevel level, CancellationToken cancellationToken = default) =>
        apps.ListByQualityLevelAsync(level, cancellationToken);

    public Task<IReadOnlyList<QualitySnapshot>> GetQualityHistoryAsync(
        string appId, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default) =>
        snapshots.GetHistoryAsync(appId, from, to, cancellationToken);
}
