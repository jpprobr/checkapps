using CheckApps.Application.Persistence;
using CheckApps.Domain.Analysis;
using CheckApps.Domain.Discovery;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.Application.Analysis;

/// <summary>
/// Leitura dos contratos (spec 021): os endpoints declarados de um app e o que mudou desde o commit analisado anteriormente. Só
/// lê: nunca dispara análise nem fala com o provedor. A comparação é calculada aqui, a cada leitura, e não é gravada.
/// </summary>
public class ContractService(IAppRepository apps, IAnalysisRepository analyses)
{
    /// <exception cref="KeyNotFoundException">O app não existe.</exception>
    public async Task<AppContracts> GetForAppAsync(string appId, CancellationToken cancellationToken = default)
    {
        await EnsureAppExistsAsync(appId, cancellationToken);

        var views = await analyses.GetCurrentContractsByAppAsync(appId, cancellationToken);
        return new AppContracts(appId, views.Select(view => new RepositoryContracts(
            view.Repository, view.Current, view.LastFailure, DeclaredIn(view.Repository), view.Endpoints)).ToList());
    }

    /// <exception cref="KeyNotFoundException">O app não existe.</exception>
    public async Task<AppContractChanges> GetChangesForAppAsync(string appId, CancellationToken cancellationToken = default)
    {
        await EnsureAppExistsAsync(appId, cancellationToken);

        var views = await analyses.GetCurrentContractsByAppAsync(appId, cancellationToken);
        var repositories = new List<RepositoryContractChanges>();

        foreach (var view in views)
        {
            var baseline = view.Current?.CommitSha is { } commit
                ? await analyses.GetContractBaselineAsync(view.Repository.Id, commit, cancellationToken)
                : null;

            if (baseline is null)
            {
                repositories.Add(new RepositoryContractChanges(
                    view.Repository, view.Current, null,
                    view.Endpoints.Count > 0 ? ContractChangeStates.FirstAnalysis : ContractChangeStates.NoContract, [], []));
                continue;
            }

            var anyPartial = view.Current!.Status == AnalysisStatuses.Partial || baseline.Analysis.Status == AnalysisStatuses.Partial;
            var comparison = ContractComparer.Compare(baseline.Endpoints, view.Endpoints, anyPartial);
            repositories.Add(new RepositoryContractChanges(
                view.Repository, view.Current, baseline.Analysis, ContractChangeStates.Compared, comparison.Changes, comparison.Warnings));
        }

        return new AppContractChanges(appId, repositories);
    }

    private async Task EnsureAppExistsAsync(string appId, CancellationToken cancellationToken)
    {
        if (await apps.GetAsync(appId, cancellationToken) is null)
        {
            throw new KeyNotFoundException($"App '{appId}' não encontrado.");
        }
    }

    /// <summary>Derivado do papel do repositório. O dado não diz se o contrato é de um gateway: a tela não afirma isso.</summary>
    private static string DeclaredIn(Repository repository) =>
        string.Equals(repository.Role, RepositoryRoles.Contract, StringComparison.OrdinalIgnoreCase)
            ? ContractDeclaredIn.ContractRepository
            : ContractDeclaredIn.ApplicationRepository;
}

/// <summary>Onde o contrato foi declarado. Texto na API.</summary>
public static class ContractDeclaredIn
{
    public const string ContractRepository = "repositorio-de-contrato";
    public const string ApplicationRepository = "repositorio-da-aplicacao";
}

/// <summary>Situação da comparação de um repositório. Texto na API.</summary>
public static class ContractChangeStates
{
    /// <summary>Nenhuma análise de commit diferente com endpoints: nada a comparar, o que não é "tudo novo".</summary>
    public const string FirstAnalysis = "primeira-analise";

    public const string Compared = "comparado";

    public const string NoContract = "sem-contrato";
}

public sealed record AppContracts(string AppId, IReadOnlyList<RepositoryContracts> Repositories);

/// <param name="Analysis">A corrente; nula se o repositório nunca foi analisado.</param>
/// <param name="DeclaredIn">Um de <see cref="ContractDeclaredIn"/>.</param>
public sealed record RepositoryContracts(
    Repository Repository, RepositoryAnalysis? Analysis, RepositoryAnalysis? LastFailure, string DeclaredIn,
    IReadOnlyList<RepositoryApiEndpoint> Endpoints);

public sealed record AppContractChanges(string AppId, IReadOnlyList<RepositoryContractChanges> Repositories);

/// <param name="Baseline">A análise anterior comparada; nula se não há base.</param>
/// <param name="State">Um de <see cref="ContractChangeStates"/>.</param>
public sealed record RepositoryContractChanges(
    Repository Repository, RepositoryAnalysis? Analysis, RepositoryAnalysis? Baseline, string State,
    IReadOnlyList<ContractChange> Changes, IReadOnlyList<string> Warnings);
