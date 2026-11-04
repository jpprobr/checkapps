using CheckApps.Application.Persistence;
using CheckApps.Domain.Analysis;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.Application.Analysis;

/// <summary>
/// Leitura das integrações declaradas (spec 014): o que um app chama e quem chama cada host. Só lê — nunca dispara
/// análise nem fala com o provedor. O host é a chave **literal** (<c>host[:porta]</c>, inclusive o modelo
/// <c>empresa.{env}.exemplo.com</c>): a agregação não casa <c>{env}</c> com <c>dev</c> ou <c>hom</c>. O app a que um host
/// pertence vem do vínculo feito por pessoa (spec 017), resolvido pelo <see cref="HostLinkResolver"/>: nunca por palpite.
/// </summary>
public class IntegrationService(
    IAppRepository apps, IAnalysisRepository analyses, IHostLinkRepository hostLinks, IHostExternalRepository hostExternals,
    AnalysisOptions options)
{
    private async Task<HostLinkResolver> BuildResolverAsync(CancellationToken cancellationToken)
    {
        var links = await hostLinks.ListAsync(cancellationToken);
        var externals = await hostExternals.ListAsync(cancellationToken);
        var names = links.Count == 0
            ? new Dictionary<string, string>()
            : (await apps.ListAllAsync(cancellationToken)).ToDictionary(a => a.Id, a => a.Name);
        return new HostLinkResolver(links, names, options.EffectiveEnvPlaceholderValues, externals);
    }

    /// <exception cref="KeyNotFoundException">O app não existe.</exception>
    public async Task<AppIntegrations> GetForAppAsync(string appId, CancellationToken cancellationToken = default)
    {
        if (await apps.GetAsync(appId, cancellationToken) is null)
        {
            throw new KeyNotFoundException($"App '{appId}' não encontrado.");
        }

        var views = await analyses.GetCurrentIntegrationsByAppAsync(appId, cancellationToken);
        var resolver = await BuildResolverAsync(cancellationToken);
        var repositories = views.Select(view => new RepositoryIntegrations(
            view.Repository, view.Current, view.LastFailure,
            view.Integrations
                .GroupBy(i => i.Reference, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => new IntegrationSection(
                    g.Key,
                    g.OrderBy(i => i.Environment is not null)            // o arquivo base primeiro
                     .ThenBy(i => i.Environment, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(i => i.ConfigKey, StringComparer.Ordinal)
                     .ToList(),
                    DestinationsOf(g, resolver),
                    ExternalsOf(g, resolver)))
                .ToList())).ToList();

        return new AppIntegrations(appId, repositories);
    }

    /// <summary>O app destino (vínculo confirmado) de cada integração com host, pelo id da integração.</summary>
    private static IReadOnlyDictionary<string, HostLinkMatch> DestinationsOf(
        IEnumerable<RepositoryIntegration> integrations, HostLinkResolver resolver)
    {
        var destinations = new Dictionary<string, HostLinkMatch>();
        foreach (var integration in integrations.Where(i => i.Host is not null))
        {
            if (resolver.Resolve(HostKey(integration)).Confirmed is { } confirmed)
            {
                destinations[integration.Id] = confirmed;
            }
        }

        return destinations;
    }

    /// <summary>A marca de externo (spec 019) de cada integração com host, pelo id da integração.</summary>
    private static IReadOnlyDictionary<string, HostExternalMatch> ExternalsOf(
        IEnumerable<RepositoryIntegration> integrations, HostLinkResolver resolver)
    {
        var result = new Dictionary<string, HostExternalMatch>();
        foreach (var integration in integrations.Where(i => i.Host is not null))
        {
            var info = resolver.Resolve(HostKey(integration));
            if (info.Confirmed is null && info.External is { } external)
            {
                result[integration.Id] = external;
            }
        }

        return result;
    }

    /// <param name="hostText">Trecho do host, sem diferença de caixa; vazio lista todos.</param>
    /// <param name="linkStatus">Um de <see cref="LinkStatuses.All"/>; nulo ou vazio traz todos.</param>
    /// <exception cref="ArgumentException"><paramref name="linkStatus"/> desconhecido.</exception>
    public async Task<HostListResult> ListHostsAsync(
        string? hostText, string? squadId, string? linkStatus = null, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(linkStatus) && !LinkStatuses.All.Contains(linkStatus.Trim()))
        {
            throw new ArgumentException(
                $"linkStatus desconhecido: \"{linkStatus}\". Use {string.Join(", ", LinkStatuses.All)}.");
        }

        var resolver = await BuildResolverAsync(cancellationToken);
        var rows = await analyses.ListIntegrationRowsAsync(
            string.IsNullOrWhiteSpace(hostText) ? null : hostText.Trim(),
            string.IsNullOrWhiteSpace(squadId) ? null : squadId, cancellationToken);
        var coverage = await analyses.GetCoverageAsync(cancellationToken);

        var hosts = rows
            .GroupBy(r => HostKey(r.Integration), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var first = g.First().Integration;
                return new HostSummary(
                    g.Key, first.Host!.ToLowerInvariant(), first.Port, first.Host.Contains('{'),
                    g.Select(r => r.AppId).Distinct().Count(),
                    g.Select(r => r.RepositoryId).Distinct().Count(),
                    g.Where(r => r.SquadId is not null).Select(r => (r.SquadId!, r.SquadName ?? r.SquadId!)).Distinct().OrderBy(s => s.Item2, StringComparer.OrdinalIgnoreCase).ToList(),
                    g.Select(r => r.Integration.Reference).Distinct(StringComparer.Ordinal).Order(StringComparer.OrdinalIgnoreCase).ToList(),
                    g.Select(r => r.Integration.EnvironmentKind).Where(k => k is not null).Select(k => k!).Distinct().Order().ToList(),
                    g.Max(r => r.AnalyzedAt),
                    resolver.Resolve(g.Key));
            })
            .Where(h => string.IsNullOrWhiteSpace(linkStatus) || h.Link.Status == linkStatus.Trim())
            .OrderByDescending(h => h.AppCount)
            .ThenByDescending(h => h.RepositoryCount)
            .ThenBy(h => h.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new HostListResult(hosts, coverage);
    }

    /// <param name="hostKey"><c>host</c> ou <c>host:porta</c>, como na lista.</param>
    /// <exception cref="KeyNotFoundException">Nenhuma análise corrente chama esse host.</exception>
    public async Task<HostDetail> GetHostAsync(string hostKey, CancellationToken cancellationToken = default)
    {
        var (host, _) = SplitKey(hostKey);
        var rows = (await analyses.ListIntegrationRowsAsync(host, null, cancellationToken))
            .Where(r => string.Equals(HostKey(r.Integration), hostKey.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (rows.Count == 0)
        {
            throw new KeyNotFoundException($"Nenhuma integração com o host '{hostKey}' foi encontrada nas análises correntes.");
        }

        var first = rows[0].Integration;
        var resolver = await BuildResolverAsync(cancellationToken);
        return new HostDetail(
            HostKey(first), first.Host!.ToLowerInvariant(), first.Port, first.Host.Contains('{'),
            rows.OrderBy(r => r.AppName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.RepositoryName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Integration.Reference, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Integration.Environment is not null)
                .ThenBy(r => r.Integration.Environment, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            resolver.Resolve(HostKey(first)));
    }

    /// <summary>A chave do host na lista e na URL: <c>host</c> em minúsculas, mais <c>:porta</c> quando não é a padrão.</summary>
    public static string HostKey(RepositoryIntegration integration) =>
        integration.Port is { } port ? $"{integration.Host!.ToLowerInvariant()}:{port}" : integration.Host!.ToLowerInvariant();

    private static (string Host, int? Port) SplitKey(string key)
    {
        var trimmed = key.Trim();
        var colon = trimmed.LastIndexOf(':');
        return colon > 0 && int.TryParse(trimmed[(colon + 1)..], out var port) ? (trimmed[..colon], port) : (trimmed, null);
    }
}

/// <summary>As integrações de uma seção de configuração (ex.: <c>IPagamentosServiceCliente</c>), com o arquivo base primeiro.</summary>
/// <param name="Destinations">Por id da integração: o app destino quando o host tem vínculo confirmado (spec 017).</param>
/// <param name="Externals">Por id da integração: a marca de externo quando o host é de um serviço fora do catálogo e não tem app (spec 019).</param>
public record IntegrationSection(
    string Reference, IReadOnlyList<RepositoryIntegration> Integrations, IReadOnlyDictionary<string, HostLinkMatch> Destinations,
    IReadOnlyDictionary<string, HostExternalMatch> Externals);

/// <param name="Analysis">A corrente; nula se o repositório nunca foi analisado.</param>
/// <param name="LastFailure">Tentativa que falhou depois da corrente.</param>
public record RepositoryIntegrations(
    Repository Repository, RepositoryAnalysis? Analysis, RepositoryAnalysis? LastFailure, IReadOnlyList<IntegrationSection> Sections);

public record AppIntegrations(string AppId, IReadOnlyList<RepositoryIntegrations> Repositories);

/// <param name="Key"><c>host[:porta]</c> literal; é a identidade do host nesta spec.</param>
/// <param name="IsTemplate">O host tem <c>{env}</c>: depende do ambiente e não é um host concreto.</param>
public record HostSummary(
    string Key, string Host, int? Port, bool IsTemplate, int AppCount, int RepositoryCount,
    IReadOnlyList<(string Id, string Name)> Squads, IReadOnlyList<string> References, IReadOnlyList<string> EnvironmentKinds,
    DateTimeOffset LatestAnalyzedAt, HostLinkInfo Link);

/// <param name="Coverage">Quantos repositórios nunca foram analisados (a lista pode estar incompleta) e a data da análise mais recente.</param>
public record HostListResult(IReadOnlyList<HostSummary> Hosts, AnalysisCoverage Coverage);

public record HostDetail(
    string Key, string Host, int? Port, bool IsTemplate, IReadOnlyList<IntegrationUsageRow> Usages, HostLinkInfo Link);
