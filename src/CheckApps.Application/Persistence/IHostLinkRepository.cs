using CheckApps.Domain.Analysis;

namespace CheckApps.Application.Persistence;

/// <summary>Persistência dos vínculos host→app (spec 017). Independe das análises.</summary>
public interface IHostLinkRepository
{
    Task<IReadOnlyList<HostLink>> ListAsync(CancellationToken cancellationToken = default);

    Task<HostLink?> GetAsync(string hostKey, string appId, CancellationToken cancellationToken = default);

    /// <summary>Cria o vínculo ou, se o par (host, app) já existe, troca o estado, a data e a nota.</summary>
    Task UpsertAsync(HostLink link, CancellationToken cancellationToken = default);

    /// <returns><c>false</c> se o vínculo não existia.</returns>
    Task<bool> DeleteAsync(string hostKey, string appId, CancellationToken cancellationToken = default);
}
