using CheckApps.Domain;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.Application.Persistence;

public interface IAppRepository
{
    Task<App?> GetAsync(string appId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<App>> ListBySquadAsync(string squadId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<App>> ListByQualityLevelAsync(QualityLevel level, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Repository>> GetRepositoriesAsync(string appId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SonarProject>> GetSonarProjectsAsync(string appId, CancellationToken cancellationToken = default);

    Task SaveAsync(App app, CancellationToken cancellationToken = default);

    Task AddRepositoryAsync(Repository repository, CancellationToken cancellationToken = default);

    Task AddSonarProjectAsync(SonarProject sonarProject, CancellationToken cancellationToken = default);
}
