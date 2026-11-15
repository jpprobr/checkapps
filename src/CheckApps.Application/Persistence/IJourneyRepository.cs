using CheckApps.Domain;

namespace CheckApps.Application.Persistence;

/// <summary>
/// Persistência das jornadas e dos passos (spec 022). Primitivos "burros": as regras (ciclo, nome entre irmãos, profundidade, passos
/// só em folha) são do <c>JourneyService</c>. O número de jornadas é pequeno, então a árvore é montada em memória.
/// </summary>
public interface IJourneyRepository
{
    /// <summary>Todas as jornadas, com tags, em ordem de id.</summary>
    Task<IReadOnlyList<Journey>> ListAsync(CancellationToken cancellationToken = default);

    Task<Journey?> GetAsync(string journeyId, CancellationToken cancellationToken = default);

    /// <summary>Grava a jornada e as tags numa transação; <c>false</c> se já existe uma jornada com esse id.</summary>
    Task<bool> TryCreateAsync(Journey journey, CancellationToken cancellationToken = default);

    /// <summary>Atualiza os campos e substitui as tags. O id nunca muda.</summary>
    Task UpdateAsync(Journey journey, CancellationToken cancellationToken = default);

    /// <summary>Remove a jornada; os passos e as tags dela vão junto.</summary>
    Task DeleteAsync(string journeyId, CancellationToken cancellationToken = default);

    /// <summary>Os passos da jornada, em ordem de posição.</summary>
    Task<IReadOnlyList<JourneyStep>> GetStepsAsync(string journeyId, CancellationToken cancellationToken = default);

    /// <summary>Os passos de todas as jornadas, para contagens e para a subárvore.</summary>
    Task<IReadOnlyList<JourneyStep>> ListAllStepsAsync(CancellationToken cancellationToken = default);

    /// <summary>Substitui a lista inteira de passos da jornada, numa transação.</summary>
    Task ReplaceStepsAsync(string journeyId, IReadOnlyList<JourneyStep> steps, CancellationToken cancellationToken = default);

    /// <summary>Quantas jornadas a squad tem como dona (guarda de remoção da squad).</summary>
    Task<int> CountOwnedBySquadAsync(string squadId, CancellationToken cancellationToken = default);

    /// <summary>Algum passo de alguma jornada usa o app (guarda de remoção do app)?</summary>
    Task<bool> AnyStepUsesAppAsync(string appId, CancellationToken cancellationToken = default);
}
