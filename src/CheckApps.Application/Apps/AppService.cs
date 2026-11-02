using CheckApps.Application.Persistence;
using CheckApps.Domain;
using CheckApps.Domain.Discovery;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.Application.Apps;

/// <summary>Campos editáveis de um repositório cadastrado à mão.</summary>
public record RepositoryInput(
    string? Name, string? Provider, string? Url, string? Status, string? Sigla, string? SiglaApp, string? Role);

public class AppService(
    IAppRepository apps, ISquadRepository squads, IQualitySnapshotRepository snapshots, ICheckRepository checks)
{
    public Task<App?> GetAppAsync(string appId, CancellationToken cancellationToken = default) =>
        apps.GetAsync(appId, cancellationToken);

    public Task<IReadOnlyList<Repository>> ListRepositoriesAsync(string appId, CancellationToken cancellationToken = default) =>
        apps.GetRepositoriesAsync(appId, cancellationToken);

    public Task<IReadOnlyList<CatalogRepository>> ListCatalogRepositoriesAsync(
        string? sigla, CancellationToken cancellationToken = default) =>
        apps.ListCatalogOnlyRepositoriesAsync(string.IsNullOrWhiteSpace(sigla) ? null : sigla, cancellationToken);

    public async Task<Repository> CreateRepositoryAsync(
        string appId, RepositoryInput input, CancellationToken cancellationToken = default)
    {
        await EnsureAppExistsAsync(appId, cancellationToken);

        var repository = new Repository
        {
            Id = Guid.NewGuid().ToString("n"),
            AppId = appId,
            Name = RequireName(input.Name),
            Source = RepositorySources.Manual,
        };
        Apply(repository, input);
        await EnsureSiglaAppFreeAsync(repository, cancellationToken);
        await EnsureNoOtherActiveInRoleAsync(repository, cancellationToken);
        await apps.AddRepositoryAsync(repository, cancellationToken);
        return repository;
    }

    public async Task<Repository> UpdateRepositoryAsync(
        string appId, string repositoryId, RepositoryInput input, CancellationToken cancellationToken = default)
    {
        var repository = await apps.GetRepositoryAsync(appId, repositoryId, cancellationToken)
            ?? throw new KeyNotFoundException($"Repositório '{repositoryId}' não encontrado no app '{appId}'.");
        repository.Name = RequireName(input.Name);
        Apply(repository, input);
        await EnsureSiglaAppFreeAsync(repository, cancellationToken);
        await EnsureNoOtherActiveInRoleAsync(repository, cancellationToken);
        await apps.AddRepositoryAsync(repository, cancellationToken);
        return repository;
    }

    /// <summary>
    /// Marca o repositório como ativo (spec 012). Recusa com 409 se o app já tem outro ativo no mesmo papel: nenhum
    /// repositório é rebaixado sozinho — a pessoa marca o outro como <c>legacy</c> antes.
    /// </summary>
    public async Task<Repository> ActivateRepositoryAsync(
        string appId, string repositoryId, CancellationToken cancellationToken = default)
    {
        var repository = await apps.GetRepositoryAsync(appId, repositoryId, cancellationToken)
            ?? throw new KeyNotFoundException($"Repositório '{repositoryId}' não encontrado no app '{appId}'.");
        repository.Status = RepositoryStatuses.Active;
        await EnsureNoOtherActiveInRoleAsync(repository, cancellationToken);
        await apps.AddRepositoryAsync(repository, cancellationToken);
        return repository;
    }

    /// <summary>
    /// Regra do repositório ativo (spec 012): no máximo um ativo por papel dentro do app — o papel ausente é um grupo
    /// só. Se <paramref name="repository"/> é ativo e outro já ocupa o grupo, lança <see cref="DomainConflictException"/>
    /// nomeando-o; nada é gravado nem rebaixado. Usada também pela triagem ao vincular um descoberto a um app.
    /// </summary>
    public async Task EnsureNoOtherActiveInRoleAsync(Repository repository, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(repository.Status, RepositoryStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var occupying = (await apps.GetRepositoriesAsync(repository.AppId, cancellationToken))
            .Where(other => other.Id != repository.Id
                && string.Equals(other.Status, RepositoryStatuses.Active, StringComparison.OrdinalIgnoreCase)
                && string.Equals(other.Role ?? string.Empty, repository.Role ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            .Select(other => $"'{other.Name}'")
            .ToList();
        if (occupying.Count == 0)
        {
            return;
        }

        var role = string.IsNullOrWhiteSpace(repository.Role) ? "sem papel" : repository.Role;
        throw new DomainConflictException(
            $"O repositório {string.Join(", ", occupying)} já é o ativo do papel '{role}' neste app. Marque-o como legacy antes de ativar '{repository.Name}'.");
    }

    public async Task DeleteRepositoryAsync(string appId, string repositoryId, CancellationToken cancellationToken = default)
    {
        if (await apps.GetRepositoryAsync(appId, repositoryId, cancellationToken) is null)
        {
            throw new KeyNotFoundException($"Repositório '{repositoryId}' não encontrado no app '{appId}'.");
        }

        await apps.DeleteRepositoryAsync(appId, repositoryId, cancellationToken);
    }

    public Task<IReadOnlyList<SonarProject>> ListSonarProjectsAsync(string appId, CancellationToken cancellationToken = default) =>
        apps.GetSonarProjectsAsync(appId, cancellationToken);

    public Task<IReadOnlyList<App>> ListByQualityLevelAsync(QualityLevel level, CancellationToken cancellationToken = default) =>
        apps.ListByQualityLevelAsync(level, cancellationToken);

    public Task<IReadOnlyList<App>> ListAllAsync(CancellationToken cancellationToken = default) =>
        apps.ListAllAsync(cancellationToken);

    public Task<IReadOnlyList<QualitySnapshot>> GetQualityHistoryAsync(
        string appId, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default) =>
        snapshots.GetHistoryAsync(appId, from, to, cancellationToken);

    public async Task<App> CreateAsync(
        string name, string squadId, string? description, string? criticality,
        IReadOnlyCollection<string>? tags, string? externalId, CancellationToken cancellationToken = default)
    {
        await EnsureSquadExistsAsync(squadId, cancellationToken);

        App? created = null;

        await SlugIdGenerator.GenerateUniqueAsync(name, async (candidateId, ct) =>
        {
            var app = new App
            {
                Id = candidateId,
                SquadId = squadId,
                Name = name,
                Description = description,
                Criticality = criticality,
                Tags = tags ?? [],
                Status = "active",
                ExternalId = externalId
            };

            if (!await apps.TryCreateAsync(app, ct))
            {
                return false;
            }

            created = app;
            return true;
        }, cancellationToken);

        return created!;
    }

    public async Task<App> UpdateAsync(
        string appId, string name, string squadId, string? description, string? criticality,
        IReadOnlyCollection<string>? tags, string? status, string? externalId, CancellationToken cancellationToken = default)
    {
        var app = await apps.GetAsync(appId, cancellationToken)
            ?? throw new KeyNotFoundException($"App '{appId}' não encontrado.");

        if (squadId != app.SquadId)
        {
            await EnsureSquadExistsAsync(squadId, cancellationToken);
        }

        // Muta o objeto carregado (não cria um App novo): preserva os campos
        // desnormalizados de quality state que SaveAsync grava de volta junto.
        app.Name = name;
        app.SquadId = squadId;
        app.Description = description;
        app.Criticality = criticality;
        app.Tags = tags ?? [];
        app.Status = status;
        app.ExternalId = externalId;

        await apps.SaveAsync(app, cancellationToken);
        return app;
    }

    public async Task DeleteAsync(string appId, CancellationToken cancellationToken = default)
    {
        var latestSnapshot = await snapshots.GetLatestAsync(appId, cancellationToken);
        if (latestSnapshot is not null)
        {
            throw new DomainConflictException(
                $"App '{appId}' possui histórico de qualidade e não pode ser removido. Inative o app em vez de removê-lo.");
        }

        // Participação em checagem também é histórico (spec 003, FR-014). A FK de
        // check_items.app_id recusaria de qualquer forma; aqui vira 409 com mensagem.
        if (await checks.IsAppInAnyCheckAsync(appId, cancellationToken))
        {
            throw new DomainConflictException(
                $"App '{appId}' está em uma ou mais checagens e não pode ser removido. Inative o app em vez de removê-lo.");
        }

        await apps.DeleteAllAsync(appId, cancellationToken);
    }

    public Task<IReadOnlyList<AppLink>> ListLinksAsync(string appId, CancellationToken cancellationToken = default) =>
        apps.GetLinksAsync(appId, cancellationToken);

    public async Task<AppLink> AddLinkAsync(
        string appId, string description, string uri, CancellationToken cancellationToken = default)
    {
        await EnsureAppExistsAsync(appId, cancellationToken);
        var link = new AppLink { Id = Guid.NewGuid().ToString("n"), AppId = appId, Description = description, Uri = uri };
        await apps.SaveLinkAsync(link, cancellationToken);
        return link;
    }

    public async Task<AppLink> UpdateLinkAsync(
        string appId, string linkId, string description, string uri, CancellationToken cancellationToken = default)
    {
        await EnsureAppExistsAsync(appId, cancellationToken);
        var link = new AppLink { Id = linkId, AppId = appId, Description = description, Uri = uri };
        await apps.SaveLinkAsync(link, cancellationToken);
        return link;
    }

    public Task DeleteLinkAsync(string appId, string linkId, CancellationToken cancellationToken = default) =>
        apps.DeleteLinkAsync(appId, linkId, cancellationToken);

    private async Task EnsureAppExistsAsync(string appId, CancellationToken cancellationToken)
    {
        if (await apps.GetAsync(appId, cancellationToken) is null)
        {
            throw new KeyNotFoundException($"App '{appId}' não encontrado.");
        }
    }

    private static string RequireName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("O nome do repositório é obrigatório.") : name.Trim();

    /// <summary>Copia os campos editáveis. <c>Source</c> e <c>ExternalId</c> nunca mudam por aqui.</summary>
    private static void Apply(Repository repository, RepositoryInput input)
    {
        repository.Provider = Clean(input.Provider);
        repository.Url = Clean(input.Url);
        repository.Status = Clean(input.Status)?.ToLowerInvariant() ?? RepositoryStatuses.Active;
        repository.Sigla = Clean(input.Sigla)?.ToLowerInvariant();
        repository.SiglaApp = SiglaAppConvention.Normalize(input.SiglaApp);
        repository.Role = Clean(input.Role);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task EnsureSiglaAppFreeAsync(Repository repository, CancellationToken cancellationToken)
    {
        if (repository.SiglaApp is not null
            && await apps.IsSiglaAppInUseAsync(repository.SiglaApp, repository.Id, cancellationToken))
        {
            throw new DomainConflictException($"A sigla-app '{repository.SiglaApp}' já pertence a outro repositório do catálogo.");
        }
    }

    private async Task EnsureSquadExistsAsync(string squadId, CancellationToken cancellationToken)
    {
        if (await squads.GetAsync(squadId, cancellationToken) is null)
        {
            throw new KeyNotFoundException($"Squad '{squadId}' não encontrada.");
        }
    }
}
