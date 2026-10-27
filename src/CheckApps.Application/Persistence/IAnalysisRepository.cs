using CheckApps.Domain.Analysis;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.Application.Persistence;

/// <summary>
/// Persistência das análises de repositório (spec 013). A análise **corrente** de um repositório é a mais recente
/// que não falhou; uma falha fica no histórico, mas nunca a esconde.
/// </summary>
public interface IAnalysisRepository
{
    /// <summary>Grava a análise e os fatos dela numa transação só.</summary>
    Task AddAsync(
        RepositoryAnalysis analysis, IReadOnlyList<RepositoryDependency> facts, CancellationToken cancellationToken = default);

    /// <summary>A análise corrente do repositório (a mais recente que não falhou), ou <c>null</c> se nunca foi analisado.</summary>
    Task<RepositoryAnalysis?> GetCurrentAsync(string repositoryId, CancellationToken cancellationToken = default);

    /// <summary>Todos os repositórios do app, cada um com a análise corrente, a última falha mais nova que ela e os fatos.</summary>
    Task<IReadOnlyList<RepositoryAnalysisView>> GetCurrentByAppAsync(string appId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pacotes das análises correntes, com o app, a squad e a situação do repositório. <paramref name="name"/> nulo traz
    /// todos os pacotes (quem filtra, por prefixo de nome, é o serviço); com nome, a comparação ignora a caixa.
    /// </summary>
    Task<IReadOnlyList<PackageUsageRow>> SearchPackagesAsync(
        string? name, string? squadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uma linha por app e framework dos repositórios **ativos** nas análises correntes; app sem informação aparece
    /// com <c>Framework = null</c>. Apps inativos ficam de fora.
    /// </summary>
    Task<IReadOnlyList<FrameworkUsageRow>> ListFrameworkUsageAsync(CancellationToken cancellationToken = default);

    Task<AnalysisCoverage> GetCoverageAsync(CancellationToken cancellationToken = default);
}

/// <param name="Current">Nula se o repositório nunca teve uma análise que não falhou.</param>
/// <param name="LastFailure">A tentativa que falhou depois da corrente (ou sem nenhuma corrente); nula se não há.</param>
public record RepositoryAnalysisView(
    Repository Repository, RepositoryAnalysis? Current, RepositoryAnalysis? LastFailure, IReadOnlyList<RepositoryDependency> Facts);

public record PackageUsageRow(
    RepositoryDependency Fact, string RepositoryId, string RepositoryName, string? RepositoryStatus,
    string AppId, string AppName, string? SquadId, string? SquadName, DateTimeOffset AnalyzedAt);

public record FrameworkUsageRow(
    string AppId, string AppName, string? SquadId, string? SquadName, string? Framework, string? RepositoryName);

/// <param name="UnanalyzedRepositories">Repositórios do catálogo que nunca tiveram uma análise que não falhou.</param>
/// <param name="LatestAnalyzedAt">Data da análise mais recente do catálogo inteiro.</param>
public record AnalysisCoverage(int UnanalyzedRepositories, DateTimeOffset? LatestAnalyzedAt);
