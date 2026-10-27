using CheckApps.Application.Persistence;
using CheckApps.Domain.Analysis;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.Application.Analysis;

/// <summary>
/// Leitura das integrações declaradas (spec 014): o que um app chama e quem chama cada host. Só lê — nunca dispara
/// análise nem fala com o provedor. O host é a chave **literal** (<c>host[:porta]</c>, inclusive o modelo
/// <c>empresa.{env}.exemplo.com</c>): não se casa <c>{env}</c> com <c>dev</c> ou <c>hom</c> por palpite, e esta spec não
/// diz a que app do catálogo um host pertence (isso é a fatia seguinte).
/// </summary>
public class IntegrationService(IAppRepository apps, IAnalysisRepository analyses)
{
    /// <exception cref="KeyNotFoundException">O app não existe.</exception>
    public async Task<AppIntegrations> GetForAppAsync(string appId, CancellationToken cancellationToken = default)
    {
        if (await apps.GetAsync(appId, cancellationToken) is null)
        {
            throw new KeyNotFoundException($"App '{appId}' não encontrado.");
        }

        var views = await analyses.GetCurrentIntegrationsByAppAsync(appId, cancellationToken);
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
                     .ToList()))
                .ToList())).ToList();

        return new AppIntegrations(appId, repositories);
    }

    /// <param name="hostText">Trecho do host, sem diferença de caixa; vazio lista todos.</param>
    public async Task<HostListResult> ListHostsAsync(
        string? hostText, string? squadId, CancellationToken cancellationToken = default)
    {
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
                    g.Max(r => r.AnalyzedAt));
            })
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
        return new HostDetail(
            HostKey(first), first.Host!.ToLowerInvariant(), first.Port, first.Host.Contains('{'),
            rows.OrderBy(r => r.AppName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.RepositoryName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Integration.Reference, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Integration.Environment is not null)
                .ThenBy(r => r.Integration.Environment, StringComparer.OrdinalIgnoreCase)
                .ToList());
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
public record IntegrationSection(string Reference, IReadOnlyList<RepositoryIntegration> Integrations);

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
    DateTimeOffset LatestAnalyzedAt);

/// <param name="Coverage">Quantos repositórios nunca foram analisados (a lista pode estar incompleta) e a data da análise mais recente.</param>
public record HostListResult(IReadOnlyList<HostSummary> Hosts, AnalysisCoverage Coverage);

public record HostDetail(string Key, string Host, int? Port, bool IsTemplate, IReadOnlyList<IntegrationUsageRow> Usages);
