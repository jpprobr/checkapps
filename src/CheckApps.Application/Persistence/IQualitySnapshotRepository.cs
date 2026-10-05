using CheckApps.Domain;

namespace CheckApps.Application.Persistence;

public interface IQualitySnapshotRepository
{
    /// <summary>
    /// Registra um novo snapshot e atualiza atomicamente o estado atual do App
    /// (currentQualityLevel/currentMetrics/lastSnapshotAt e o GSI2PK de quality level).
    /// A atualização do estado atual só é aplicada se este snapshot for mais recente
    /// que o último já registrado (evita regressão em backfills fora de ordem).
    /// </summary>
    Task AddAsync(QualitySnapshot snapshot, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QualitySnapshot>> GetHistoryAsync(
        string appId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);

    Task<QualitySnapshot?> GetLatestAsync(string appId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Histórico de todos os apps de uma squad, numa consulta só. Existe para que a evolução
    /// agregada não faça uma consulta por app (N+1).
    /// </summary>
    Task<IReadOnlyList<QualitySnapshot>> GetHistoryBySquadAsync(
        string squadId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);
}
