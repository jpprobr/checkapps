using CheckApps.Domain;

namespace CheckApps.Application.Persistence;

public interface ISquadRepository
{
    Task<IReadOnlyList<Squad>> ListAsync(CancellationToken cancellationToken = default);

    Task<Squad?> GetAsync(string squadId, CancellationToken cancellationToken = default);

    Task SaveAsync(Squad squad, CancellationToken cancellationToken = default);
}
