using CheckApps.Application.Analysis;
using CheckApps.Application.Persistence;
using CheckApps.Domain;
using CheckApps.Domain.Analysis;

namespace CheckApps.UnitTests.Analysis;

/// <summary>Spec 020: a montagem do grafo do mapa a partir dos fatos já lidos (função pura, sem I/O).</summary>
public class MapServiceTests
{
    private static readonly string[] Env = ["dev", "hom", "prod"];
    private static readonly DateTimeOffset At = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    private static readonly AnalysisCoverage Coverage = new(0, null);
    private static readonly IReadOnlyDictionary<string, string> Squads = new Dictionary<string, string> { ["sq-1"] = "Squad 1", ["sq-2"] = "Squad 2" };

    private static App AppOf(string id, string squadId = "sq-1", QualityLevel? level = null) =>
        new() { Id = id, SquadId = squadId, Name = $"App {id}", CurrentQualityLevel = level };

    private static IntegrationUsageRow Row(string appId, string host, string? kind = null, string squadId = "sq-1") =>
        new(new RepositoryIntegration
            {
                Id = Guid.NewGuid().ToString("n"), AnalysisId = "an", Mechanism = IntegrationMechanisms.Config, Scheme = "https",
                Host = host, Reference = "ISvc", ConfigKey = "ISvc:BaseUrl", EnvironmentKind = kind,
                Environment = kind, State = IntegrationStates.Resolved, ProjectPath = "src", SourceFile = "src/appsettings.json",
                Confidence = Confidences.High, ScannerId = "dotnet", ScannerVersion = "1.1.0"
            },
            "repo", "repo-name", "active", appId, $"App {appId}", squadId, Squads[squadId], At);

    private static HostLink Link(string host, string appId, string state = HostLinkStates.Confirmed) =>
        new() { Id = $"l-{host}-{appId}", HostKey = host, AppId = appId, State = state, DecidedAt = At };

    private static HostExternal External(string host, string? name = null) =>
        new() { Id = $"e-{host}", HostKey = host, Name = name, DecidedAt = At };

    private static AppMap Build(
        IReadOnlyList<IntegrationUsageRow> rows, IReadOnlyList<HostLink> links, IReadOnlyList<HostExternal> externals,
        IReadOnlyList<App> apps, string? scope = null, IReadOnlyList<NoScannerRepositoryRow>? noScanner = null) =>
        MapService.Build(
            rows, new HostLinkResolver(links, apps.ToDictionary(a => a.Id, a => a.Name), Env, externals), apps, Squads, scope,
            Coverage, noScanner ?? []);

    [Fact]
    public void ConfirmedHost_BecomesAnEdgeBetweenApps_PerEnvironment_AggregatedByCount()
    {
        var map = Build(
            [Row("a", "b.hom.exemplo.com", "hom"), Row("a", "b.hom.exemplo.com", "hom"), Row("a", "b.prod.exemplo.com", "prod")],
            [Link("b.{env}.exemplo.com", "b")], [], [AppOf("a"), AppOf("b")]);

        Assert.Equal(2, map.Edges.Count);
        var hom = Assert.Single(map.Edges, e => e.EnvironmentKind == "hom");
        Assert.Equal(("a", "b", 2), (hom.FromId, hom.ToId, hom.Count));
        Assert.Equal(["b.hom.exemplo.com"], hom.Hosts);
        Assert.Contains(map.Edges, e => e.EnvironmentKind == "prod" && e.Count == 1);
        Assert.All(map.Nodes, n => Assert.Equal(MapNodeKinds.App, n.Kind));
    }

    [Fact]
    public void BaseFileAndUnknownEnvironmentIntegrations_HaveANullEnvironment()
    {
        var map = Build([Row("a", "b.exemplo.com", null)], [Link("b.exemplo.com", "b")], [], [AppOf("a"), AppOf("b")]);

        Assert.Null(Assert.Single(map.Edges).EnvironmentKind);
    }

    [Fact]
    public void ExternalHost_GoesToANamedExternalNode_AndNoDecisionGoesToTheSingleNoAppNode()
    {
        var map = Build(
            [Row("a", "x.parceira.com"), Row("a", "y.parceira.com"), Row("a", "z.sem-decisao.com"), Row("a", "w.sem-decisao.com")],
            [Link("w.sem-decisao.com", "b", HostLinkStates.Rejected)],
            [External("x.parceira.com", "Squad Parceira"), External("y.parceira.com", "squad parceira")], [AppOf("a"), AppOf("b")]);

        var external = Assert.Single(map.Nodes, n => n.Kind == MapNodeKinds.External);
        Assert.Equal("Squad Parceira", external.Label);                 // o mesmo nome, em caixa diferente, é um nó só
        var noApp = Assert.Single(map.Nodes, n => n.Kind == MapNodeKinds.NoApp);
        var toNoApp = Assert.Single(map.Edges, e => e.ToId == noApp.Id);
        Assert.Equal(2, toNoApp.Count);                                  // sem decisão e só rejeitado
        Assert.Equal(2, Assert.Single(map.Edges, e => e.ToId == external.Id).Count);
    }

    [Fact]
    public void ExternalWithoutName_UsesTheHostKeyAsTheLabel()
    {
        var map = Build([Row("a", "x.parceira.com")], [], [External("x.parceira.com")], [AppOf("a")]);

        Assert.Equal("x.parceira.com", Assert.Single(map.Nodes, n => n.Kind == MapNodeKinds.External).Label);
    }

    [Fact]
    public void AppCallingItsOwnHost_IsFlaggedAsASelfLoop()
    {
        var map = Build([Row("a", "a.exemplo.com")], [Link("a.exemplo.com", "a")], [], [AppOf("a")]);

        var edge = Assert.Single(map.Edges);
        Assert.True(edge.SelfLoop);
        Assert.Equal(edge.FromId, edge.ToId);
    }

    [Fact]
    public void Nodes_CarrySquadAndQualityLevel_AndAppsWithoutIntegrationsAreIsolatedNodes()
    {
        var map = Build([Row("a", "b.exemplo.com")], [Link("b.exemplo.com", "b")], [],
            [AppOf("a", level: QualityLevel.A), AppOf("b", "sq-2", QualityLevel.C), AppOf("c")]);

        Assert.Equal(("Squad 1", "A"), (map.Nodes.Single(n => n.Id == "a").SquadName, map.Nodes.Single(n => n.Id == "a").QualityLevel));
        Assert.Equal("C", map.Nodes.Single(n => n.Id == "b").QualityLevel);
        Assert.Contains(map.Nodes, n => n.Id == "c");
        Assert.DoesNotContain(map.Edges, e => e.FromId == "c");
    }

    [Fact]
    public void SquadScope_KeepsOnlyThatSquadsAppsAsNodes_PlusTheEdgeEndpoints()
    {
        var map = Build([Row("a", "b.exemplo.com")], [Link("b.exemplo.com", "b")], [],
            [AppOf("a"), AppOf("b", "sq-2"), AppOf("c", "sq-2")], scope: "sq-1");

        Assert.Equal(["a", "b"], map.Nodes.Select(n => n.Id).Order());   // c é de outra squad e não tem aresta
    }

    [Fact]
    public void Coverage_CarriesTheNoScannerRepositories()
    {
        var map = Build([], [], [], [AppOf("a")], noScanner: [new NoScannerRepositoryRow("r1", "infra-repo", "a", "App a")]);

        Assert.Equal("infra-repo", Assert.Single(map.Coverage.NoScannerRepositories).RepositoryName);
    }
}
