using CheckApps.Domain.Analysis;

namespace CheckApps.Application.Persistence;

/// <summary>Persistência dos hosts marcados como externos ao catálogo (spec 019). Independe das análises.</summary>
public interface IHostExternalRepository
{
    Task<IReadOnlyList<HostExternal>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Cria a marca ou, se o host já está marcado, troca o nome, a data e a nota.</summary>
    Task UpsertAsync(HostExternal external, CancellationToken cancellationToken = default);

    /// <returns><c>false</c> se o host não estava marcado.</returns>
    Task<bool> DeleteAsync(string hostKey, CancellationToken cancellationToken = default);
}
