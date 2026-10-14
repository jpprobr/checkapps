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
    /// Descobertos por status e sigla (<c>null</c> em qualquer filtro significa "todos"), ordenados por
    /// sigla e nome. <paramref name="scope"/> diz quais: por padrão, só os que ainda **não** estão no
    /// catálogo (os que aguardam decisão). "No catálogo" é derivado — existe um Repository com o mesmo
    /// provedor e id externo —, nunca um status armazenado.
    /// </summary>
    Task<IReadOnlyList<DiscoveredRepository>> ListAsync(
        TriageStatus? status, string? sigla, CatalogScope scope = CatalogScope.Pending,
        CancellationToken cancellationToken = default);

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

/// <summary>Quais descobertos a listagem devolve, quanto ao catálogo.</summary>
public enum CatalogScope
{
    /// <summary>Só os que ainda não estão no catálogo — os que aguardam decisão (o comportamento original).</summary>
    Pending,

    /// <summary>Só os que já estão no catálogo.</summary>
    Cataloged,

    /// <summary>Todos, estejam ou não no catálogo.</summary>
    Any
}

public enum UpsertOutcome
{
    New,
    Updated,
    Unchanged
}
