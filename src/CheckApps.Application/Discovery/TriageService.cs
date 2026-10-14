using CheckApps.Application.Apps;
using CheckApps.Application.Persistence;
using CheckApps.Domain;
using CheckApps.Domain.Discovery;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.Application.Discovery;

/// <summary>
/// A decisão da pessoa sobre cada repositório descoberto (spec 005). Só a confirmação humana cria
/// App ou Repository — o scan nunca faz isso. Exceções seguem a convenção dos outros serviços:
/// <see cref="KeyNotFoundException"/> para o descoberto endereçado (404), <see cref="ArgumentException"/>
/// para input inválido do cliente — nome vazio, squad ou app inexistente (400) — e
/// <see cref="DomainConflictException"/> (409).
/// </summary>
public class TriageService(
    IDiscoveredRepositoryRepository discovered, IAppRepository apps, ISquadRepository squads,
    AppService appService, ScanOptions options)
{
    /// <summary>
    /// Os descobertos com a sugestão e, quando já estão no catálogo, o app em que estão. Por padrão só os
    /// que aguardam decisão; <see cref="CatalogScope.Cataloged"/> e <see cref="CatalogScope.Any"/> trazem
    /// também os que já viraram app (a tela "Repositórios" mostra todos, cada um com o seu estado).
    /// </summary>
    public async Task<IReadOnlyList<DiscoveredItem>> ListAsync(
        TriageStatus? status, string? sigla, CatalogScope scope = CatalogScope.Pending,
        CancellationToken cancellationToken = default)
    {
        var repositories = await discovered.ListAsync(status, sigla, scope, cancellationToken);

        // Os que aguardam decisão, por definição, não estão no catálogo: nada a consultar.
        var catalogByProvider = new Dictionary<string, IReadOnlyDictionary<string, CatalogApp>>();
        if (scope != CatalogScope.Pending)
        {
            foreach (var group in repositories.GroupBy(r => r.Provider))
            {
                catalogByProvider[group.Key] = await discovered.GetCatalogAppsAsync(
                    group.Key, group.Select(r => r.ExternalId).ToList(), cancellationToken);
            }
        }

        return repositories
            .Select(r => new DiscoveredItem(
                r, Suggest(r),
                catalogByProvider.TryGetValue(r.Provider, out var map) && map.TryGetValue(r.ExternalId, out var app) ? app : null))
            .ToList();
    }

    /// <summary>Cria o App e vincula o repositório. Sequencial: app, depois repositório (ver spec 005, edge cases).</summary>
    public async Task<App> CreateAppAsync(
        string discoveredId, string name, string squadId, string? description, string? role,
        CancellationToken cancellationToken = default)
    {
        var repository = await GetOrThrowAsync(discoveredId, cancellationToken);
        await EnsureNotInCatalogAsync(repository, cancellationToken);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("O nome do app é obrigatório.");
        }

        if (await squads.GetAsync(squadId, cancellationToken) is null)
        {
            throw new ArgumentException($"Squad '{squadId}' não encontrada.");
        }

        // Antes de criar o app: se a sigla-app já é de outro repositório, não sobra app sem repositório.
        await EnsureSiglaAppFreeAsync(repository, cancellationToken);

        var app = await appService.CreateAsync(name.Trim(), squadId, description, null, null, null, cancellationToken);
        await apps.AddRepositoryAsync(ToRepository(app.Id, repository, role ?? Suggest(repository).Role), cancellationToken);
        return app;
    }

    /// <summary>Vincula a um app existente. Sigla diferente da dos demais repositórios do app é aviso, não bloqueio.</summary>
    public async Task<IReadOnlyList<string>> LinkAsync(
        string discoveredId, string appId, string? role, CancellationToken cancellationToken = default)
    {
        var repository = await GetOrThrowAsync(discoveredId, cancellationToken);
        await EnsureNotInCatalogAsync(repository, cancellationToken);

        if (await apps.GetAsync(appId, cancellationToken) is null)
        {
            throw new ArgumentException($"App '{appId}' não encontrado.");
        }

        await EnsureSiglaAppFreeAsync(repository, cancellationToken);

        var warnings = new List<string>();
        var otherSiglas = (await apps.GetRepositoriesAsync(appId, cancellationToken))
            .Select(r => r.Sigla)
            .Where(s => !string.IsNullOrWhiteSpace(s) && !string.Equals(s, repository.Sigla, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();
        if (otherSiglas.Count > 0)
        {
            warnings.Add(
                $"O app '{appId}' já tem repositórios na sigla {string.Join(", ", otherSiglas)}; este é da sigla '{repository.Sigla}'. Os repositórios de um app costumam ficar na mesma sigla.");
        }

        await apps.AddRepositoryAsync(ToRepository(appId, repository, role ?? Suggest(repository).Role), cancellationToken);
        return warnings;
    }

    public Task MarkOtherSquadAsync(string discoveredId, CancellationToken cancellationToken = default) =>
        SetStatusAsync(discoveredId, TriageStatus.OtherSquad, cancellationToken);

    public Task IgnoreAsync(string discoveredId, CancellationToken cancellationToken = default) =>
        SetStatusAsync(discoveredId, TriageStatus.Ignored, cancellationToken);

    public Task RestoreAsync(string discoveredId, CancellationToken cancellationToken = default) =>
        SetStatusAsync(discoveredId, TriageStatus.New, cancellationToken);

    private async Task SetStatusAsync(string discoveredId, TriageStatus status, CancellationToken cancellationToken)
    {
        var repository = await GetOrThrowAsync(discoveredId, cancellationToken);
        await EnsureNotInCatalogAsync(repository, cancellationToken);
        await discovered.SetStatusAsync(discoveredId, status, cancellationToken);
    }

    private async Task<DiscoveredRepository> GetOrThrowAsync(string discoveredId, CancellationToken cancellationToken) =>
        await discovered.GetAsync(discoveredId, cancellationToken)
            ?? throw new KeyNotFoundException($"Repositório descoberto '{discoveredId}' não encontrado.");

    private async Task EnsureNotInCatalogAsync(DiscoveredRepository repository, CancellationToken cancellationToken)
    {
        if (await discovered.IsInCatalogAsync(repository.Provider, repository.ExternalId, cancellationToken))
        {
            throw new DomainConflictException($"O repositório '{repository.Name}' já está no catálogo.");
        }
    }

    private async Task EnsureSiglaAppFreeAsync(DiscoveredRepository repository, CancellationToken cancellationToken)
    {
        if (repository.SiglaApp is not null && await discovered.IsSiglaAppInUseAsync(repository.SiglaApp, cancellationToken))
        {
            throw new DomainConflictException(
                $"A sigla-app '{repository.SiglaApp}' já pertence a outro repositório do catálogo.");
        }
    }

    private RepositorySuggestion Suggest(DiscoveredRepository repository) =>
        string.IsNullOrWhiteSpace(options.NameTemplate)
            ? RepositoryNameConvention.WithoutConvention(repository.Name)
            : new RepositoryNameConvention(options.NameTemplate).Suggest(repository.Name, repository.Sigla, repository.SiglaApp);

    private static Repository ToRepository(string appId, DiscoveredRepository repository, string? role) => new()
    {
        Id = Guid.NewGuid().ToString("n"),
        AppId = appId,
        Name = repository.Name,
        Provider = repository.Provider,
        Url = repository.Url,
        ExternalId = repository.ExternalId,
        Status = repository.Archived ? "archived" : "active",
        Sigla = repository.Sigla,
        SiglaApp = repository.SiglaApp,
        Role = string.IsNullOrWhiteSpace(role) ? null : role.Trim(),
        Source = repository.Source
    };
}

/// <summary>Um descoberto com a sugestão calculada na leitura (nunca armazenada).</summary>
/// <param name="CatalogApp">Preenchido quando o repositório já está no catálogo (só nas listagens que o incluem).</param>
public record DiscoveredItem(DiscoveredRepository Repository, RepositorySuggestion Suggestion, CatalogApp? CatalogApp = null);
