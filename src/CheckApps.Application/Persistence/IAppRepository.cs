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

    /// <summary>Put condicional (attribute_not_exists(PK)): false se já existir um App com esse id.</summary>
    Task<bool> TryCreateAsync(App app, CancellationToken cancellationToken = default);

    /// <summary>Apaga a partição inteira do App (METADATA, repositórios, projetos Sonar e links). Não usar se houver QualitySnapshots.</summary>
    Task DeleteAllAsync(string appId, CancellationToken cancellationToken = default);

    Task AddRepositoryAsync(Repository repository, CancellationToken cancellationToken = default);

    /// <summary>
    /// Repositórios do catálogo que **não** têm descoberto correspondente (mesmo provedor e id externo): os
    /// cadastrados à mão e os do seed. Os que vieram do scan já aparecem pela caixa de entrada.
    /// </summary>
    Task<IReadOnlyList<CatalogRepository>> ListCatalogOnlyRepositoriesAsync(
        string? sigla, CancellationToken cancellationToken = default);

    Task<Repository?> GetRepositoryAsync(string appId, string repositoryId, CancellationToken cancellationToken = default);

    Task DeleteRepositoryAsync(string appId, string repositoryId, CancellationToken cancellationToken = default);

    /// <summary>Algum repositório que não <paramref name="exceptRepositoryId"/> já usa essa sigla-app?</summary>
    Task<bool> IsSiglaAppInUseAsync(string siglaApp, string? exceptRepositoryId, CancellationToken cancellationToken = default);

    Task AddSonarProjectAsync(SonarProject sonarProject, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppLink>> GetLinksAsync(string appId, CancellationToken cancellationToken = default);

    Task SaveLinkAsync(AppLink link, CancellationToken cancellationToken = default);

    Task DeleteLinkAsync(string appId, string linkId, CancellationToken cancellationToken = default);
}

/// <summary>Um repositório do catálogo com o app (e a squad do app) a que pertence.</summary>
public record CatalogRepository(Repository Repository, string AppId, string AppName, string? SquadId = null, string? SquadName = null);
