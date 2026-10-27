using CheckApps.Application.Persistence;
using CheckApps.Domain.Analysis;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.Application.Analysis;

/// <summary>
/// Leitura das análises (spec 013): o que um app usa, quem usa um pacote e a versão do .NET por squad. Só lê —
/// nunca dispara análise nem fala com o provedor. "Interno" é calculado aqui, a cada leitura, a partir de
/// <see cref="AnalysisOptions.InternalPackagePrefixes"/>: nunca fica gravado.
/// </summary>
public class DependencyService(IAppRepository apps, IAnalysisRepository analyses, AnalysisOptions options)
{
    private bool InternalPrefixesConfigured =>
        options.InternalPackagePrefixes.Any(p => InternalPackage.NormalizePrefix(p) is not null);

    private bool IsInternal(string name) => InternalPackage.IsInternal(name, options.InternalPackagePrefixes);

    /// <exception cref="KeyNotFoundException">O app não existe.</exception>
    public async Task<AppDependencies> GetForAppAsync(string appId, CancellationToken cancellationToken = default)
    {
        if (await apps.GetAsync(appId, cancellationToken) is null)
        {
            throw new KeyNotFoundException($"App '{appId}' não encontrado.");
        }

        var views = await analyses.GetCurrentByAppAsync(appId, cancellationToken);
        var repositories = views.Select(view => new RepositoryDependencies(
            view.Repository,
            view.Current,
            view.LastFailure,
            view.Facts.Where(f => f.Kind == FactKinds.Framework).Select(ToFact).ToList(),
            view.Facts.Where(f => f.Kind == FactKinds.Package).Select(ToFact).ToList())).ToList();

        return new AppDependencies(appId, InternalPrefixesConfigured, repositories);
    }

    /// <param name="name">Nome exato do pacote, sem diferença de caixa. Pode faltar só com <paramref name="internalOnly"/>.</param>
    /// <param name="internalOnly">Só pacotes internos (prefixo configurado). Sem nome, lista os pacotes internos em uso.</param>
    /// <exception cref="ArgumentException">Sem nome e sem <paramref name="internalOnly"/>.</exception>
    public async Task<PackageSearchResult> SearchPackagesAsync(
        string? name, string? squadId, bool internalOnly, CancellationToken cancellationToken = default)
    {
        var hasName = !string.IsNullOrWhiteSpace(name);
        if (!hasName && !internalOnly)
        {
            throw new ArgumentException("Informe o nome do pacote.");
        }

        var coverage = await analyses.GetCoverageAsync(cancellationToken);
        var configured = InternalPrefixesConfigured;

        // "Apenas internos" sem prefixo configurado não tem resposta possível: a tela explica o motivo.
        if (internalOnly && !configured)
        {
            return new PackageSearchResult([], [], coverage, configured);
        }

        var rows = await analyses.SearchPackagesAsync(hasName ? name!.Trim() : null, string.IsNullOrWhiteSpace(squadId) ? null : squadId, cancellationToken);
        var usages = rows
            .Select(row => new PackageUsage(row, IsInternal(row.Fact.Name)))
            .Where(usage => !internalOnly || usage.IsInternal)
            .ToList();

        // Quantos apps usam cada versão. Um app com o pacote em dois projetos conta uma vez.
        var summary = usages
            .GroupBy(u => (Name: u.Row.Fact.Name.ToLowerInvariant(), u.Row.Fact.Version))
            .Select(g => new PackageVersionSummary(
                g.First().Row.Fact.Name, g.Key.Version, g.Select(u => u.Row.AppId).Distinct().Count(), g.First().IsInternal))
            .OrderByDescending(s => s.AppCount)
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Version, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new PackageSearchResult(usages, summary, coverage, configured);
    }

    /// <summary>
    /// Framework × squad, considerando só repositórios **ativos** e apps ativos. Um app em mais de um framework conta
    /// em cada um; app sem nenhuma informação vai para o grupo <c>Framework = null</c>, não some.
    /// </summary>
    public async Task<FrameworkOverview> GetFrameworkOverviewAsync(CancellationToken cancellationToken = default)
    {
        var rows = await analyses.ListFrameworkUsageAsync(cancellationToken);
        var coverage = await analyses.GetCoverageAsync(cancellationToken);

        var cells = new List<FrameworkCell>();
        foreach (var app in rows.GroupBy(r => r.AppId))
        {
            var first = app.First();
            var frameworks = app.Where(r => r.Framework is not null).Select(r => r.Framework!).Distinct().ToList();

            if (frameworks.Count == 0)
            {
                cells.Add(new FrameworkCell(null, first.SquadId, first.SquadName, [new FrameworkApp(first.AppId, first.AppName, [])]));
                continue;
            }

            foreach (var framework in frameworks)
            {
                var repositories = app.Where(r => r.Framework == framework && r.RepositoryName is not null)
                    .Select(r => r.RepositoryName!).Distinct().ToList();
                cells.Add(new FrameworkCell(framework, first.SquadId, first.SquadName, [new FrameworkApp(first.AppId, first.AppName, repositories)]));
            }
        }

        var grouped = cells
            .GroupBy(c => (c.Framework, c.SquadId))
            .Select(g => new FrameworkCell(g.Key.Framework, g.Key.SquadId, g.First().SquadName, g.SelectMany(c => c.Apps).ToList()))
            .OrderBy(c => c.Framework is null)                       // "sem informação" por último
            .ThenByDescending(c => c.Framework, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.SquadName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new FrameworkOverview(grouped, coverage);
    }

    private DependencyFact ToFact(RepositoryDependency fact) =>
        new(fact, fact.Kind == FactKinds.Package && IsInternal(fact.Name));
}

public record DependencyFact(RepositoryDependency Fact, bool IsInternal);

/// <param name="Analysis">A corrente; nula se o repositório nunca foi analisado.</param>
/// <param name="LastFailure">Tentativa que falhou depois da corrente.</param>
public record RepositoryDependencies(
    Repository Repository, RepositoryAnalysis? Analysis, RepositoryAnalysis? LastFailure,
    IReadOnlyList<DependencyFact> Frameworks, IReadOnlyList<DependencyFact> Packages);

/// <param name="InternalPrefixesConfigured">Sem prefixo configurado nada é marcado como interno; a tela pode dizer isso.</param>
public record AppDependencies(string AppId, bool InternalPrefixesConfigured, IReadOnlyList<RepositoryDependencies> Repositories);

public record PackageUsage(PackageUsageRow Row, bool IsInternal);

public record PackageVersionSummary(string Name, string? Version, int AppCount, bool IsInternal);

/// <param name="Coverage">Quantos repositórios nunca foram analisados (a resposta pode estar incompleta) e a data da análise mais recente.</param>
public record PackageSearchResult(
    IReadOnlyList<PackageUsage> Usages, IReadOnlyList<PackageVersionSummary> Versions, AnalysisCoverage Coverage,
    bool InternalPrefixesConfigured);

/// <param name="Framework">Nulo = "sem informação".</param>
public record FrameworkCell(string? Framework, string? SquadId, string? SquadName, IReadOnlyList<FrameworkApp> Apps);

public record FrameworkApp(string AppId, string AppName, IReadOnlyList<string> Repositories);

public record FrameworkOverview(IReadOnlyList<FrameworkCell> Cells, AnalysisCoverage Coverage);
