using CheckApps.Domain.Analysis;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.Application.Persistence;

/// <summary>
/// Persistência das análises de repositório (spec 013). A análise **corrente** de um repositório é a mais recente
/// que não falhou; uma falha fica no histórico, mas nunca a esconde.
/// </summary>
public interface IAnalysisRepository
{
    /// <summary>Grava a análise e os fatos dela, de qualquer tipo, numa transação só (spec 021).</summary>
    Task SaveAsync(RepositoryAnalysis analysis, IReadOnlyList<IAnalysisFact> facts, CancellationToken cancellationToken = default);

    /// <summary>A análise corrente do repositório (a mais recente que não falhou), ou <c>null</c> se nunca foi analisado.</summary>
    Task<RepositoryAnalysis?> GetCurrentAsync(string repositoryId, CancellationToken cancellationToken = default);

    /// <summary>Todos os repositórios do app, cada um com a análise corrente, a última falha mais nova que ela e os fatos.</summary>
    Task<IReadOnlyList<RepositoryAnalysisView>> GetCurrentByAppAsync(string appId, CancellationToken cancellationToken = default);

    /// <summary>Como <see cref="GetCurrentByAppAsync"/>, com as integrações da análise corrente em vez dos fatos.</summary>
    Task<IReadOnlyList<RepositoryIntegrationsView>> GetCurrentIntegrationsByAppAsync(
        string appId, CancellationToken cancellationToken = default);

    /// <summary>Como <see cref="GetCurrentByAppAsync"/>, com os endpoints declarados e seus campos (spec 021).</summary>
    Task<IReadOnlyList<RepositoryContractsView>> GetCurrentContractsByAppAsync(string appId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A base da comparação de contratos: a análise não falhada mais recente do repositório, de commit diferente de
    /// <paramref name="currentCommitSha"/>, que tenha endpoints, com eles. Nula se não há.
    /// </summary>
    Task<ContractSnapshot?> GetContractBaselineAsync(string repositoryId, string currentCommitSha, CancellationToken cancellationToken = default);

    /// <summary>
    /// Integrações **com host** das análises correntes, com o repositório, o app e a squad. <paramref name="hostContains"/>
    /// filtra por trecho do host, sem diferença de caixa; nulo traz todas. O agrupamento por host é do serviço.
    /// </summary>
    Task<IReadOnlyList<IntegrationUsageRow>> ListIntegrationRowsAsync(
        string? hostContains, string? squadId, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Repositórios cuja análise corrente tem a situação <c>sem-scanner</c> (nenhum scanner se aplica), com o app.
    /// <paramref name="squadId"/> restringe aos apps da squad; nulo traz todos.
    /// </summary>
    Task<IReadOnlyList<NoScannerRepositoryRow>> ListNoScannerRepositoriesAsync(
        string? squadId, CancellationToken cancellationToken = default);
}

public record NoScannerRepositoryRow(string RepositoryId, string RepositoryName, string AppId, string AppName);

/// <param name="Current">Nula se o repositório nunca teve uma análise que não falhou.</param>
/// <param name="LastFailure">A tentativa que falhou depois da corrente (ou sem nenhuma corrente); nula se não há.</param>
public record RepositoryAnalysisView(
    Repository Repository, RepositoryAnalysis? Current, RepositoryAnalysis? LastFailure, IReadOnlyList<RepositoryDependency> Facts);

/// <param name="Current">Nula se o repositório nunca teve uma análise que não falhou.</param>
public record RepositoryIntegrationsView(
    Repository Repository, RepositoryAnalysis? Current, RepositoryAnalysis? LastFailure, IReadOnlyList<RepositoryIntegration> Integrations);

/// <param name="Current">Nula se o repositório nunca teve uma análise que não falhou.</param>
public record RepositoryContractsView(
    Repository Repository, RepositoryAnalysis? Current, RepositoryAnalysis? LastFailure, IReadOnlyList<RepositoryApiEndpoint> Endpoints);

/// <summary>Uma foto do contrato: uma análise e os endpoints dela.</summary>
public record ContractSnapshot(RepositoryAnalysis Analysis, IReadOnlyList<RepositoryApiEndpoint> Endpoints);

public record IntegrationUsageRow(
    RepositoryIntegration Integration, string RepositoryId, string RepositoryName, string? RepositoryStatus,
    string AppId, string AppName, string? SquadId, string? SquadName, DateTimeOffset AnalyzedAt);

public record PackageUsageRow(
    RepositoryDependency Fact, string RepositoryId, string RepositoryName, string? RepositoryStatus,
    string AppId, string AppName, string? SquadId, string? SquadName, DateTimeOffset AnalyzedAt);

public record FrameworkUsageRow(
    string AppId, string AppName, string? SquadId, string? SquadName, string? Framework, string? RepositoryName);

/// <param name="UnanalyzedRepositories">Repositórios do catálogo que nunca tiveram uma análise que não falhou.</param>
/// <param name="LatestAnalyzedAt">Data da análise mais recente do catálogo inteiro.</param>
public record AnalysisCoverage(int UnanalyzedRepositories, DateTimeOffset? LatestAnalyzedAt);
