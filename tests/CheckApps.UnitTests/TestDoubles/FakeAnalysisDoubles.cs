using CheckApps.Application.Analysis;
using CheckApps.Application.Discovery;
using CheckApps.Application.Persistence;
using CheckApps.Domain.Analysis;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.UnitTests.TestDoubles;

/// <summary>
/// Análises em memória, só o que o <see cref="AnalysisService"/> usa: gravar e achar a corrente (a mais recente que não
/// falhou). As consultas de leitura são testadas contra o SQLite de verdade, nos testes de integração.
/// </summary>
public class FakeAnalysisRepository : IAnalysisRepository
{
    private readonly List<(RepositoryAnalysis Analysis, IReadOnlyList<IAnalysisFact> Facts)> _stored = [];

    public IReadOnlyList<RepositoryAnalysis> Analyses => _stored.Select(s => s.Analysis).ToList();

    public IReadOnlyList<IAnalysisFact> AllFacts => _stored.SelectMany(s => s.Facts).ToList();

    public IReadOnlyList<RepositoryDependency> Facts => _stored.SelectMany(s => s.Facts).OfType<RepositoryDependency>().ToList();

    public IReadOnlyList<RepositoryIntegration> Integrations => _stored.SelectMany(s => s.Facts).OfType<RepositoryIntegration>().ToList();

    public Task SaveAsync(RepositoryAnalysis analysis, IReadOnlyList<IAnalysisFact> facts, CancellationToken ct = default)
    {
        _stored.Add((analysis, facts));
        return Task.CompletedTask;
    }

    public Task<RepositoryAnalysis?> GetCurrentAsync(string repositoryId, CancellationToken ct = default) =>
        Task.FromResult(_stored
            .Select(s => s.Analysis)
            .Where(a => a.RepositoryId == repositoryId && a.Status != AnalysisStatuses.Failed)
            .OrderByDescending(a => a.AnalyzedAt)
            .FirstOrDefault());

    public Task<IReadOnlyList<RepositoryAnalysisView>> GetCurrentByAppAsync(string appId, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<RepositoryIntegrationsView>> GetCurrentIntegrationsByAppAsync(string appId, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<RepositoryContractsView>> GetCurrentContractsByAppAsync(string appId, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<ContractSnapshot?> GetContractBaselineAsync(string repositoryId, string currentCommitSha, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<IntegrationUsageRow>> ListIntegrationRowsAsync(string? hostContains, string? squadId, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<PackageUsageRow>> SearchPackagesAsync(string? name, string? squadId, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<FrameworkUsageRow>> ListFrameworkUsageAsync(CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<AnalysisCoverage> GetCoverageAsync(CancellationToken ct = default) => throw new NotSupportedException();

    public Task<IReadOnlyList<NoScannerRepositoryRow>> ListNoScannerRepositoriesAsync(string? squadId, CancellationToken ct = default) =>
        throw new NotSupportedException();
}

/// <summary>
/// Fonte de conteúdo em memória: cada repositório tem um commit e seus arquivos. Conta as leituras e pode falhar de
/// propósito, para os testes provarem que um commit já analisado não lê nada e que uma falha não afeta os outros.
/// </summary>
public class FakeContentSource : IRepositoryContentSource
{
    private readonly Dictionary<string, (string Sha, Dictionary<string, string> Files, bool Truncated)> _repositories = [];
    private readonly Dictionary<string, string> _failures = [];
    private readonly HashSet<string> _vanished = [];

    public string? ConfigurationProblem { get; set; }

    /// <summary>Provedor que esta fonte lê.</summary>
    public string Provider { get; set; } = "fake";

    public int ListCalls { get; private set; }

    public int ReadCalls { get; private set; }

    public List<string> ReadPaths { get; } = [];

    public FakeContentSource With(string repositoryId, string sha, Dictionary<string, string> files, bool truncated = false)
    {
        _repositories[repositoryId] = (sha, files, truncated);
        return this;
    }

    public FakeContentSource Failing(string repositoryId, string message)
    {
        _failures[repositoryId] = message;
        return this;
    }

    /// <summary>O arquivo aparece na árvore, mas já não existe quando é lido (mudou entre as duas chamadas).</summary>
    public FakeContentSource Vanishing(string path)
    {
        _vanished.Add(path);
        return this;
    }

    public bool CanRead(Repository repository) => repository.Provider == Provider;

    public Task<RepositoryTree> ListAsync(Repository repository, CancellationToken cancellationToken = default)
    {
        ListCalls++;
        if (_failures.TryGetValue(repository.Id, out var message))
        {
            throw new RepositorySourceException(message);
        }

        var (sha, files, truncated) = _repositories[repository.Id];
        return Task.FromResult(new RepositoryTree(
            sha, files.Select(f => new RepositoryFile(f.Key, f.Value.Length)).ToList(), truncated));
    }

    public Task<string?> ReadFileAsync(
        Repository repository, string commitSha, string path, CancellationToken cancellationToken = default)
    {
        ReadCalls++;
        ReadPaths.Add(path);
        var (_, files, _) = _repositories[repository.Id];
        return Task.FromResult(_vanished.Contains(path) ? null : files.GetValueOrDefault(path));
    }
}

/// <summary>Vínculos host→app em memória (spec 017); a chave do host é comparada em minúsculas, como no SQLite.</summary>
public class FakeHostLinkRepository : IHostLinkRepository
{
    private readonly List<HostLink> _links = [];

    public IReadOnlyList<HostLink> Links => _links;

    public Task<IReadOnlyList<HostLink>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<HostLink>>(_links.OrderBy(l => l.HostKey, StringComparer.Ordinal).ThenBy(l => l.AppId, StringComparer.Ordinal).ToList());

    public Task<HostLink?> GetAsync(string hostKey, string appId, CancellationToken ct = default) =>
        Task.FromResult(_links.FirstOrDefault(l => l.HostKey == hostKey.ToLowerInvariant() && l.AppId == appId));

    public Task UpsertAsync(HostLink link, CancellationToken ct = default)
    {
        _links.RemoveAll(l => l.HostKey == link.HostKey.ToLowerInvariant() && l.AppId == link.AppId);
        _links.Add(link with { HostKey = link.HostKey.ToLowerInvariant() });
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string hostKey, string appId, CancellationToken ct = default) =>
        Task.FromResult(_links.RemoveAll(l => l.HostKey == hostKey.ToLowerInvariant() && l.AppId == appId) > 0);
}

/// <summary>Hosts externos ao catálogo em memória (spec 019); a chave do host é comparada em minúsculas, como no SQLite.</summary>
public class FakeHostExternalRepository : IHostExternalRepository
{
    private readonly List<HostExternal> _externals = [];

    public IReadOnlyList<HostExternal> Externals => _externals;

    public Task<IReadOnlyList<HostExternal>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<HostExternal>>(_externals.OrderBy(e => e.HostKey, StringComparer.Ordinal).ToList());

    public Task UpsertAsync(HostExternal external, CancellationToken ct = default)
    {
        _externals.RemoveAll(e => e.HostKey == external.HostKey.ToLowerInvariant());
        _externals.Add(external with { HostKey = external.HostKey.ToLowerInvariant() });
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string hostKey, CancellationToken ct = default) =>
        Task.FromResult(_externals.RemoveAll(e => e.HostKey == hostKey.ToLowerInvariant()) > 0);
}
