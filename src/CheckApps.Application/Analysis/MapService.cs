using CheckApps.Application.Persistence;
using CheckApps.Domain;

namespace CheckApps.Application.Analysis;

/// <summary>
/// Mapa de aplicações (spec 020): o grafo "quem chama quem", **derivado** das integrações declaradas (spec 014) e das
/// decisões humanas host→app (specs 017 e 019). Não tem tabela própria e não adivinha nada: sem decisão, o destino é
/// "sem app". O ambiente de cada aresta vem do <c>environmentKind</c> da integração (arquivo de origem), nunca do nome
/// do host; a integração do arquivo base ou de um ambiente desconhecido fica com ambiente nulo. Só lê.
/// </summary>
public class MapService(IntegrationService integrations, IAppRepository apps, IAnalysisRepository analyses)
{
    /// <param name="squadId">Restringe aos apps **de origem** da squad (os destinos podem ser de qualquer squad); nulo traz todos.</param>
    public async Task<AppMap> GetAsync(string? squadId, CancellationToken cancellationToken = default)
    {
        var scope = string.IsNullOrWhiteSpace(squadId) ? null : squadId.Trim();
        var resolver = await integrations.BuildResolverAsync(cancellationToken);
        var rows = await analyses.ListIntegrationRowsAsync(null, scope, cancellationToken);
        var allApps = await apps.ListAllAsync(cancellationToken);
        var squadNames = rows
            .Where(r => r.SquadId is not null)
            .Select(r => (Id: r.SquadId!, Name: r.SquadName ?? r.SquadId!))
            .Distinct()
            .ToDictionary(s => s.Id, s => s.Name);
        var coverage = await analyses.GetCoverageAsync(cancellationToken);
        var noScanner = await analyses.ListNoScannerRepositoriesAsync(scope, cancellationToken);

        return Build(rows, resolver, allApps, squadNames, scope, coverage, noScanner);
    }

    /// <summary>Monta o grafo a partir dos fatos já lidos (puro, sem I/O).</summary>
    public static AppMap Build(
        IReadOnlyList<IntegrationUsageRow> rows, HostLinkResolver resolver, IReadOnlyList<App> allApps,
        IReadOnlyDictionary<string, string> squadNames, string? scopeSquadId, AnalysisCoverage coverage,
        IReadOnlyList<NoScannerRepositoryRow> noScanner)
    {
        var appsById = allApps.ToDictionary(a => a.Id);
        var nodes = new Dictionary<string, MapNode>();
        var edges = new Dictionary<(string From, string To, string? Env), EdgeAccumulator>();

        // Os apps do escopo aparecem mesmo sem integração (nó isolado); os demais, só se forem origem ou destino de uma aresta.
        foreach (var app in allApps.Where(a => scopeSquadId is null || a.SquadId == scopeSquadId))
        {
            nodes[app.Id] = AppNode(app, squadNames);
        }

        foreach (var row in rows)
        {
            var key = IntegrationService.HostKey(row.Integration);
            var info = resolver.Resolve(key);
            var from = row.AppId;
            if (!nodes.ContainsKey(from))
            {
                nodes[from] = appsById.TryGetValue(from, out var source)
                    ? AppNode(source, squadNames)
                    : new MapNode(from, MapNodeKinds.App, row.AppName, row.SquadId, row.SquadName, null);
            }

            string to;
            if (info.Confirmed is { } confirmed)
            {
                to = confirmed.AppId;
                if (!nodes.ContainsKey(to))
                {
                    nodes[to] = appsById.TryGetValue(to, out var target)
                        ? AppNode(target, squadNames)
                        : new MapNode(to, MapNodeKinds.App, confirmed.AppName, null, null, null);
                }
            }
            else if (info.External is { } external)
            {
                var label = string.IsNullOrWhiteSpace(external.Name) ? external.HostKey : external.Name!;
                to = $"{MapNodeKinds.External}:{label.ToLowerInvariant()}";
                nodes.TryAdd(to, new MapNode(to, MapNodeKinds.External, label, null, null, null));
            }
            else
            {
                to = MapNodeKinds.NoApp;
                nodes.TryAdd(to, new MapNode(to, MapNodeKinds.NoApp, "Sem app", null, null, null));
            }

            var edgeKey = (from, to, row.Integration.EnvironmentKind);
            if (!edges.TryGetValue(edgeKey, out var edge))
            {
                edges[edgeKey] = edge = new EdgeAccumulator();
            }

            edge.Count++;
            edge.Hosts.Add(key);
        }

        return new AppMap(
            nodes.Values
                .OrderBy(n => n.Kind, StringComparer.Ordinal)
                .ThenBy(n => n.Label, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            edges
                .Select(e => new MapEdge(
                    e.Key.From, e.Key.To, e.Key.Env, e.Value.Count,
                    e.Value.Hosts.Order(StringComparer.OrdinalIgnoreCase).ToList(), e.Key.From == e.Key.To))
                .OrderBy(e => e.FromId, StringComparer.Ordinal)
                .ThenBy(e => e.ToId, StringComparer.Ordinal)
                .ThenBy(e => e.EnvironmentKind, StringComparer.Ordinal)
                .ToList(),
            new MapCoverage(coverage.UnanalyzedRepositories, coverage.LatestAnalyzedAt, noScanner));
    }

    private static MapNode AppNode(App app, IReadOnlyDictionary<string, string> squadNames) =>
        new(app.Id, MapNodeKinds.App, app.Name, app.SquadId,
            squadNames.TryGetValue(app.SquadId, out var name) ? name : null, app.CurrentQualityLevel?.ToString());

    private sealed class EdgeAccumulator
    {
        public int Count { get; set; }

        public HashSet<string> Hosts { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>Tipos de nó do mapa. <c>sem-app</c> é um nó só, que reúne todos os hosts sem decisão.</summary>
public static class MapNodeKinds
{
    public const string App = "app";
    public const string External = "externo";
    public const string NoApp = "sem-app";
}

/// <param name="Id">O id do app; <c>externo:&lt;nome&gt;</c> para um serviço externo; <c>sem-app</c> para o nó que reúne os hosts sem decisão.</param>
/// <param name="QualityLevel">A letra do nível de qualidade atual (só para app), ou nula.</param>
public record MapNode(string Id, string Kind, string Label, string? SquadId, string? SquadName, string? QualityLevel);

/// <param name="EnvironmentKind"><c>dev</c>, <c>hom</c>, <c>prod</c>; nulo = arquivo base ou ambiente desconhecido (vale para todos).</param>
/// <param name="Count">Quantas integrações declaradas formam esta aresta.</param>
/// <param name="Hosts">Os hosts (chave literal) que ligam os dois nós.</param>
/// <param name="SelfLoop">O app chama o próprio host (o front decide se mostra).</param>
public record MapEdge(
    string FromId, string ToId, string? EnvironmentKind, int Count, IReadOnlyList<string> Hosts, bool SelfLoop);

/// <param name="NoScannerRepositories">Repositórios para os quais nenhum scanner se aplica: o mapa não os enxerga.</param>
public record MapCoverage(
    int UnanalyzedRepositories, DateTimeOffset? LatestAnalyzedAt, IReadOnlyList<NoScannerRepositoryRow> NoScannerRepositories);

public record AppMap(IReadOnlyList<MapNode> Nodes, IReadOnlyList<MapEdge> Edges, MapCoverage Coverage);
