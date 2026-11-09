using CheckApps.Application.Analysis;
using CheckApps.Domain;
using CheckApps.Domain.Analysis;
using CheckApps.Infrastructure.Sqlite;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.IntegrationTests.Sqlite;

/// <summary>
/// Spec 020 com o SQLite de verdade: o mapa derivado das análises correntes e das decisões humanas, e a lista de
/// repositórios sem scanner. Cada teste usa uma squad própria e filtra por ela, então não depende dos outros.
/// </summary>
public class MapTests(SqliteFixture fixture) : IClassFixture<SqliteFixture>
{
    private static readonly DateTimeOffset T1 = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    private readonly SquadRepository _squads = new(fixture.ConnectionFactory);
    private readonly AppRepository _apps = new(fixture.ConnectionFactory);
    private readonly AnalysisRepository _analyses = new(fixture.ConnectionFactory, TestAnalysisWriters.All);
    private readonly HostLinkRepository _links = new(fixture.ConnectionFactory);
    private readonly HostExternalRepository _externals = new(fixture.ConnectionFactory);
    private readonly AnalysisOptions _options = new();

    private MapService Map() => new(new IntegrationService(_apps, _analyses, _links, _externals, _options), _apps, _analyses);

    private HostLinkService Links() => new(_apps, _links, _externals, _analyses, _options, TimeProvider.System);

    private async Task<string> SeedSquadAsync()
    {
        var id = $"sq-{Guid.NewGuid():N}";
        await _squads.SaveAsync(new Squad { Id = id, Name = $"Squad {id[3..9]}" });
        return id;
    }

    private async Task<(string AppId, string RepoId)> SeedAppAsync(string squadId, string name)
    {
        var appId = $"app-{Guid.NewGuid():N}";
        var repoId = $"repo-{Guid.NewGuid():N}";
        await _apps.SaveAsync(new App { Id = appId, SquadId = squadId, Name = name });
        await _apps.AddRepositoryAsync(new Repository { Id = repoId, AppId = appId, Name = $"{name}-repo", Status = "active" });
        return (appId, repoId);
    }

    private async Task AnalyzeAsync(string repoId, string status, params (string Host, string? Kind)[] calls)
    {
        var analysis = new RepositoryAnalysis
        {
            Id = Guid.NewGuid().ToString("n"), RepositoryId = repoId, AnalyzedAt = T1, CommitSha = $"sha-{Guid.NewGuid():N}", Status = status
        };
        var integrations = calls.Select(c => new RepositoryIntegration
        {
            Id = Guid.NewGuid().ToString("n"), AnalysisId = analysis.Id, Mechanism = IntegrationMechanisms.Config, Scheme = "https",
            Host = c.Host, Reference = "ISvc", ConfigKey = "ISvc:BaseUrl", Environment = c.Kind, EnvironmentKind = c.Kind,
            State = IntegrationStates.Resolved, ProjectPath = "src/App", SourceFile = "src/App/appsettings.json",
            Confidence = Confidences.High, ScannerId = "dotnet", ScannerVersion = "1.1.0"
        }).ToList();
        await _analyses.AddAsync(analysis, [], integrations);
    }

    private static string Marker() => Guid.NewGuid().ToString("n")[..8];

    [Fact]
    public async Task Map_ShowsConfirmedExternalAndUndecidedCallsByEnvironment_ScopedBySquad()
    {
        var squad = await SeedSquadAsync();
        var (caller, callerRepo) = await SeedAppAsync(squad, "Chamador");
        var (target, _) = await SeedAppAsync(squad, "Destino");
        var m = Marker();
        var targetHom = $"destino.hom.{m}.com";
        var partner = $"parceiro.{m}.com";
        var unknown = $"desconhecido.{m}.com";
        await AnalyzeAsync(callerRepo, AnalysisStatuses.Ok, (targetHom, "hom"), (partner, null), (unknown, "prod"));
        await Links().SetAsync(targetHom, target, HostLinkStates.Confirmed, null);
        await Links().SetExternalAsync(partner, "Squad Parceira", null);

        var map = await Map().GetAsync(squad);

        Assert.Contains(map.Nodes, n => n.Id == target && n.Kind == MapNodeKinds.App);
        Assert.Contains(map.Edges, e => e.FromId == caller && e.ToId == target && e.EnvironmentKind == "hom" && e.Count == 1);
        Assert.Contains(map.Edges, e => e.ToId.StartsWith("externo:") && e.EnvironmentKind is null);
        Assert.Contains(map.Edges, e => e.ToId == MapNodeKinds.NoApp && e.EnvironmentKind == "prod" && e.Hosts.Contains(unknown));
        Assert.Contains(map.Nodes, n => n.Kind == MapNodeKinds.External && n.Label == "Squad Parceira");
    }

    [Fact]
    public async Task Map_OfAnotherSquad_DoesNotShowThisSquadsCalls()
    {
        var squad = await SeedSquadAsync();
        var other = await SeedSquadAsync();
        var (_, repo) = await SeedAppAsync(squad, "Chamador");
        await AnalyzeAsync(repo, AnalysisStatuses.Ok, ($"x.{Marker()}.com", "dev"));

        var map = await Map().GetAsync(other);

        Assert.Empty(map.Edges);
        Assert.Empty(map.Nodes);
    }

    [Fact]
    public async Task NoScannerRepositories_ListsOnlyTheCurrentNoScannerAnalyses_OfTheSquad()
    {
        var squad = await SeedSquadAsync();
        var (infraApp, infraRepo) = await SeedAppAsync(squad, "Infra");
        var (_, okRepo) = await SeedAppAsync(squad, "Servico");
        var (_, neverRepo) = await SeedAppAsync(squad, "Nunca");
        await AnalyzeAsync(infraRepo, AnalysisStatuses.NoScanner);
        await AnalyzeAsync(okRepo, AnalysisStatuses.Ok);

        var map = await Map().GetAsync(squad);

        var listed = Assert.Single(map.Coverage.NoScannerRepositories);
        Assert.Equal((infraRepo, infraApp), (listed.RepositoryId, listed.AppId));
        Assert.DoesNotContain(map.Coverage.NoScannerRepositories, r => r.RepositoryId == neverRepo);
    }
}
