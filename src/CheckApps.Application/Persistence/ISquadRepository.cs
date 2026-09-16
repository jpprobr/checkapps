using CheckApps.Domain;

namespace CheckApps.Application.Persistence;

public interface ISquadRepository
{
    Task<IReadOnlyList<Squad>> ListAsync(CancellationToken cancellationToken = default);

    Task<Squad?> GetAsync(string squadId, CancellationToken cancellationToken = default);

    Task SaveAsync(Squad squad, CancellationToken cancellationToken = default);

    /// <summary>Put condicional (attribute_not_exists(PK)): false se já existir uma Squad com esse id.</summary>
    Task<bool> TryCreateAsync(Squad squad, CancellationToken cancellationToken = default);

    Task DeleteAsync(string squadId, CancellationToken cancellationToken = default);
}
