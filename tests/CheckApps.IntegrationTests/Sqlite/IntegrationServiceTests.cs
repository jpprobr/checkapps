using CheckApps.Application.Analysis;
using CheckApps.Domain;
using CheckApps.Domain.Analysis;
using CheckApps.Infrastructure.Sqlite;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.IntegrationTests.Sqlite;

/// <summary>
/// As leituras da spec 014 (integrações de um app e lista/detalhe de hosts) com o serviço e o SQLite de verdade juntos.
/// Banco só desta classe: a cobertura precisa de números exatos.
/// </summary>
public class IntegrationServiceTests(SqliteFixture fixture) : IClassFixture<SqliteFixture>
{
    private static readonly DateTimeOffset T1 = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SquadRepository _squads = new(fixture.ConnectionFactory);
    private readonly AppRepository _apps = new(fixture.ConnectionFactory);
    private readonly AnalysisRepository _analyses = new(fixture.ConnectionFactory, TestAnalysisWriters.All);

    private IntegrationService Sut() => new(_apps, _analyses, new HostLinkRepository(fixture.ConnectionFactory), new HostExternalRepository(fixture.ConnectionFactory), new AnalysisOptions());

    private async Task<string> SeedSquadAsync(string name)
    {
        var id = $"sq-{Guid.NewGuid():N}";
        await _squads.SaveAsync(new Squad { Id = id, Name = name });
        return id;
    }

    private async Task<(string AppId, string RepoId)> SeedAppAsync(string squadId, string appName)
    {
        var appId = $"app-{Guid.NewGuid():N}";
        var repoId = $"repo-{Guid.NewGuid():N}";
        await _apps.SaveAsync(new App { Id = appId, SquadId = squadId, Name = appName });
        await _apps.AddRepositoryAsync(new Repository { Id = repoId, AppId = appId, Name = $"{appName}-repo", Status = "active" });
        return (appId, repoId);
    }

    private static RepositoryIntegration Integration(
        string analysisId, string? host, string reference, string? environment = null, string? kind = null, int? port = null,
        string state = IntegrationStates.Resolved) =>
        new()
        {
            Id = Guid.NewGuid().ToString("n"), AnalysisId = analysisId, Mechanism = IntegrationMechanisms.Config,
            Scheme = host is null ? null : "https", Host = host, Port = port, BasePath = "/x", Reference = reference,
            ConfigKey = $"{reference}:BaseUrl", Environment = environment, EnvironmentKind = kind, State = state,
            ProjectPath = "src/App", SourceFile = environment is null ? "src/App/appsettings.json" : $"src/App/appsettings.{environment}.json",
            Confidence = Confidences.High, ScannerId = "dotnet", ScannerVersion = "1.1.0"
        };

    private async Task AnalyzeAsync(string repoId, DateTimeOffset at, Func<string, IEnumerable<RepositoryIntegration>> integrations)
    {
        var analysis = new RepositoryAnalysis
        {
            Id = Guid.NewGuid().ToString("n"), RepositoryId = repoId, AnalyzedAt = at, CommitSha = $"sha-{at.Ticks}", Status = AnalysisStatuses.Ok
        };
        await _analyses.AddAsync(analysis, [], integrations(analysis.Id).ToList());
    }

    // ---- Um app ----------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task ForApp_GroupsBySection_BaseFirst_AndKeepsRepositoriesWithoutAnalysis()
    {
        var squad = await SeedSquadAsync("Squad A");
        var (appId, repoId) = await SeedAppAsync(squad, "Checkout");
        await _apps.AddRepositoryAsync(new Repository { Id = $"repo-{Guid.NewGuid():N}", AppId = appId, Name = "legado", Status = "legacy" });
        await AnalyzeAsync(repoId, T1, an =>
        [
            Integration(an, "empresa.exemplo.com", "IPagamentosServiceCliente", "Production", "prod"),
            Integration(an, "empresa.{env}.exemplo.com", "IPagamentosServiceCliente", state: IntegrationStates.Parameterized),
            Integration(an, "empresa.dev.exemplo.com", "IPagamentosServiceCliente", "Development", "dev"),
            Integration(an, null, "IEstoqueServiceCliente", state: IntegrationStates.Unresolved)
        ]);

        var result = await Sut().GetForAppAsync(appId);

        Assert.Equal(2, result.Repositories.Count);
        var analyzed = result.Repositories.Single(r => r.Repository.Id == repoId);
        Assert.NotNull(analyzed.Analysis);
        Assert.Equal(["IEstoqueServiceCliente", "IPagamentosServiceCliente"], analyzed.Sections.Select(s => s.Reference));
        var payments = analyzed.Sections.Single(s => s.Reference == "IPagamentosServiceCliente");
        Assert.Equal([null, "Development", "Production"], payments.Integrations.Select(i => i.Environment));   // base primeiro

        var never = result.Repositories.Single(r => r.Repository.Name == "legado");
        Assert.Null(never.Analysis);
        Assert.Empty(never.Sections);
    }

    [Fact]
    public async Task ForApp_UnknownApp_IsNotFound() =>
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().GetForAppAsync("app-que-nao-existe"));

    // ---- Hosts -----------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Hosts_AreGroupedByTheLiteralHost_CountingAppsOnce_WithSectionsAndEnvironmentKinds()
    {
        var marker = Guid.NewGuid().ToString("n")[..8];
        var squadA = await SeedSquadAsync("Squad A");
        var squadB = await SeedSquadAsync("Squad B");
        var (appA, repoA) = await SeedAppAsync(squadA, "Checkout");
        var (appB, repoB) = await SeedAppAsync(squadB, "Faturas");
        var shared = $"pagamentos-{marker}.exemplo.com";

        await AnalyzeAsync(repoA, T1, an =>
        [
            Integration(an, shared, "IPagamentosServiceCliente", "Production", "prod"),
            Integration(an, shared.ToUpperInvariant(), "IPagamentosServiceCliente", "Staging", "hom"),   // mesma chave, outra caixa
            Integration(an, $"modelo-{marker}.{{env}}.exemplo.com", "IClientesServiceCliente", state: IntegrationStates.Parameterized)
        ]);
        await AnalyzeAsync(repoB, T1.AddHours(1), an => [Integration(an, shared, "IPagamentosApi", "Production", "prod")]);

        var result = await Sut().ListHostsAsync(marker, null);

        var sharedHost = result.Hosts.Single(h => h.Key == shared);
        Assert.Equal(2, sharedHost.AppCount);
        Assert.Equal(2, sharedHost.RepositoryCount);
        Assert.Equal(["Squad A", "Squad B"], sharedHost.Squads.Select(s => s.Name));
        Assert.Equal(["IPagamentosApi", "IPagamentosServiceCliente"], sharedHost.References);
        Assert.Equal(["hom", "prod"], sharedHost.EnvironmentKinds);
        Assert.False(sharedHost.IsTemplate);
        Assert.Equal(T1.AddHours(1), sharedHost.LatestAnalyzedAt);

        var template = result.Hosts.Single(h => h.IsTemplate);
        Assert.Equal($"modelo-{marker}.{{env}}.exemplo.com", template.Key);
        Assert.Equal(sharedHost.Key, result.Hosts[0].Key);   // o mais usado primeiro
    }

    [Fact]
    public async Task Hosts_AreNotMatchedAcrossEnvTemplateAndConcreteHosts()
    {
        var marker = Guid.NewGuid().ToString("n")[..8];
        var squad = await SeedSquadAsync("Squad A");
        var (_, repo) = await SeedAppAsync(squad, "App");
        await AnalyzeAsync(repo, T1, an =>
        [
            Integration(an, $"x-{marker}.{{env}}.exemplo.com", "ISvc", state: IntegrationStates.Parameterized),
            Integration(an, $"x-{marker}.dev.exemplo.com", "ISvc", "Development", "dev")
        ]);

        var result = await Sut().ListHostsAsync(marker, null);

        Assert.Equal(2, result.Hosts.Count);   // sem casar por palpite
    }

    [Fact]
    public async Task Hosts_KeepThePortInTheKey_AndFilterBySquad()
    {
        var marker = Guid.NewGuid().ToString("n")[..8];
        var squadA = await SeedSquadAsync("Squad A");
        var squadB = await SeedSquadAsync("Squad B");
        var (_, repoA) = await SeedAppAsync(squadA, "A");
        var (_, repoB) = await SeedAppAsync(squadB, "B");
        await AnalyzeAsync(repoA, T1, an => [Integration(an, $"api-{marker}.exemplo.com", "ISvc", port: 8443)]);
        await AnalyzeAsync(repoB, T1, an => [Integration(an, $"api-{marker}.exemplo.com", "ISvc")]);

        var all = await Sut().ListHostsAsync(marker, null);
        var onlyA = await Sut().ListHostsAsync(marker, squadA);

        Assert.Equal([$"api-{marker}.exemplo.com", $"api-{marker}.exemplo.com:8443"], all.Hosts.Select(h => h.Key).Order());
        Assert.Equal($"api-{marker}.exemplo.com:8443", Assert.Single(onlyA.Hosts).Key);
    }

    [Fact]
    public async Task Hosts_CoverageSaysHowManyRepositoriesWereNeverAnalyzed()
    {
        var squad = await SeedSquadAsync("Squad A");
        var (_, analyzed) = await SeedAppAsync(squad, "Com");
        await SeedAppAsync(squad, "Sem");
        await AnalyzeAsync(analyzed, T1, an => []);

        var before = (await Sut().ListHostsAsync(null, null)).Coverage;

        Assert.True(before.UnanalyzedRepositories >= 1);
        Assert.NotNull(before.LatestAnalyzedAt);
    }

    // ---- Detalhe ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task HostDetail_ListsEveryAppAndRepository_WithTheEvidence()
    {
        var marker = Guid.NewGuid().ToString("n")[..8];
        var squad = await SeedSquadAsync("Squad A");
        var (appId, repoId) = await SeedAppAsync(squad, "Checkout");
        var host = $"detalhe-{marker}.exemplo.com";
        await AnalyzeAsync(repoId, T1, an =>
        [
            Integration(an, host, "ISvc"),
            Integration(an, host, "ISvc", "Production", "prod", port: 8443),   // outra porta: não é este host
        ]);

        var detail = await Sut().GetHostAsync(host.ToUpperInvariant());

        Assert.Equal(host, detail.Key);
        var usage = Assert.Single(detail.Usages);
        Assert.Equal(appId, usage.AppId);
        Assert.Equal("ISvc:BaseUrl", usage.Integration.ConfigKey);
        Assert.Equal("src/App/appsettings.json", usage.Integration.SourceFile);

        var withPort = await Sut().GetHostAsync($"{host}:8443");
        Assert.Equal("Production", Assert.Single(withPort.Usages).Integration.Environment);
    }

    [Fact]
    public async Task HostDetail_UnknownHost_IsNotFound() =>
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().GetHostAsync("nao-existe.exemplo.com"));

    [Fact]
    public async Task Hosts_OnlyTheCurrentAnalysisCounts()
    {
        var marker = Guid.NewGuid().ToString("n")[..8];
        var squad = await SeedSquadAsync("Squad A");
        var (_, repoId) = await SeedAppAsync(squad, "App");
        await AnalyzeAsync(repoId, T1, an => [Integration(an, $"antigo-{marker}.exemplo.com", "ISvc")]);
        await AnalyzeAsync(repoId, T1.AddDays(1), an => [Integration(an, $"novo-{marker}.exemplo.com", "ISvc")]);

        var result = await Sut().ListHostsAsync(marker, null);

        Assert.Equal($"novo-{marker}.exemplo.com", Assert.Single(result.Hosts).Key);
    }
}
