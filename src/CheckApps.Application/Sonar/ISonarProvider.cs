using CheckApps.Domain;

namespace CheckApps.Application.Sonar;

/// <summary>
/// Porta para obter métricas de um projeto Sonar. MockSonarProvider é a única
/// implementação hoje; um SonarQubeProvider real poderá implementar a mesma porta depois.
/// </summary>
public interface ISonarProvider
{
    Task<QualityMetrics> GetCurrentMetricsAsync(string projectKey, CancellationToken cancellationToken = default);
}
