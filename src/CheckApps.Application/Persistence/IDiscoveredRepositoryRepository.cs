using CheckApps.Application.Discovery;
using CheckApps.Domain.Discovery;

namespace CheckApps.Application.Persistence;

/// <summary>Persistência da caixa de entrada do leitor de GitHub (spec 005).</summary>
public interface IDiscoveredRepositoryRepository
{
    /// <summary>
    /// Upsert por (provider, external_id): atualiza nome, url, sigla, sigla-app, arquivado, último
    /// push e <c>last_seen_at</c>; **nunca** altera o status de triagem nem <c>first_seen_at</c>.
    /// </summary>
    Task<UpsertOutcome> UpsertAsync(DiscoveredRepository repository, CancellationToken cancellationToken = default);

    Task<DiscoveredRepository?> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Descobertos que ainda **não** estão no catálogo (sem Repository com o mesmo provedor e id
    /// externo), por sigla de nome e status; <c>null</c> em qualquer filtro significa "todos".
    /// Ordenados por sigla e nome.
    /// </summary>
    Task<IReadOnlyList<DiscoveredRepository>> ListAsync(
        TriageStatus? status, string? sigla, CancellationToken cancellationToken = default);

    Task SetStatusAsync(string id, TriageStatus status, CancellationToken cancellationToken = default);

    /// <summary>Existe um Repository com esse provedor e id externo? É o que define "já está no catálogo".</summary>
    Task<bool> IsInCatalogAsync(string provider, string externalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dos ids externos informados, quais já estão no catálogo e em qual app — numa única consulta, para o
    /// resultado do scan poder dizer "já no catálogo" sem uma ida ao banco por repositório. Quem não está
    /// no catálogo simplesmente não aparece no mapa.
    /// </summary>
    Task<IReadOnlyDictionary<string, CatalogApp>> GetCatalogAppsAsync(
        string provider, IReadOnlyCollection<string> externalIds, CancellationToken cancellationToken = default);

    /// <summary>Alguma sigla-app já está em uso por um Repository? Ela identifica um único repositório.</summary>
    Task<bool> IsSiglaAppInUseAsync(string siglaApp, CancellationToken cancellationToken = default);
}

public enum UpsertOutcome
{
    New,
    Updated,
    Unchanged
}
