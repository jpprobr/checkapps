using CheckApps.Application.Persistence;
using CheckApps.Domain;
using CheckApps.Domain.Analysis;
using CheckApps.Infrastructure.Sqlite;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.IntegrationTests.Sqlite;

/// <summary>
/// A persistência das análises (spec 013) sobre SQLite de verdade. Cada teste cria os próprios squad, app e
/// repositório com ids novos; as consultas globais são conferidas só sobre o que o teste criou.
/// </summary>
public class AnalysisRepositoryTests(SqliteFixture fixture) : IClassFixture<SqliteFixture>
{
    private static readonly DateTimeOffset T1 = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = T1.AddDays(1);
    private static readonly DateTimeOffset T3 = T1.AddDays(2);

    private readonly SquadRepository _squads = new(fixture.ConnectionFactory);
    private readonly AppRepository _apps = new(fixture.ConnectionFactory);
    private readonly AnalysisRepository _analyses = new(fixture.ConnectionFactory, TestAnalysisWriters.All);

    private async Task<(string SquadId, string AppId, string RepoId)> SeedAsync(
        string repoStatus = "active", string? sigla = null, string? appStatus = null)
    {
        var squadId = $"sq-{Guid.NewGuid():N}";
        var appId = $"app-{Guid.NewGuid():N}";
        var repoId = $"repo-{Guid.NewGuid():N}";
        await _squads.SaveAsync(new Squad { Id = squadId, Name = $"Squad {squadId[3..9]}" });
        await _apps.SaveAsync(new App { Id = appId, SquadId = squadId, Name = $"app {appId[4..10]}", Status = appStatus });
        await _apps.AddRepositoryAsync(new Repository
        {
            Id = repoId, AppId = appId, Name = $"repo {repoId[5..11]}", Status = repoStatus, Sigla = sigla
        });
        return (squadId, appId, repoId);
    }

    private sealed record UnknownFact(string AnalysisId) : IAnalysisFact;

    [Fact]
    public async Task Save_FactWithoutAWriter_ThrowsAndRollsTheWholeAnalysisBack()
    {
        var (_, _, repoId) = await SeedAsync();
        var analysis = Analysis(repoId, T1);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _analyses.SaveAsync(analysis, [Fact(analysis, "Serilog"), new UnknownFact(analysis.Id)]));

        Assert.Null(await _analyses.GetCurrentAsync(repoId));
    }

    private static RepositoryAnalysis Analysis(
        string repoId, DateTimeOffset at, string status = AnalysisStatuses.Ok, string? sha = null, params string[] warnings) =>
        new()
        {
            Id = Guid.NewGuid().ToString("n"), RepositoryId = repoId, AnalyzedAt = at,
            CommitSha = sha ?? $"sha-{at:yyyyMMdd}", Status = status, Warnings = warnings
        };

    private static RepositoryDependency Fact(
        RepositoryAnalysis analysis, string name, string? version = "1.0.0", string kind = FactKinds.Package,
        string confidence = Confidences.High, string project = "src/App/App.csproj") =>
        new()
        {
            Id = Guid.NewGuid().ToString("n"), AnalysisId = analysis.Id,
            Ecosystem = kind == FactKinds.Framework ? Ecosystems.DotNet : Ecosystems.NuGet, Kind = kind,
            Name = name, Version = version, ProjectPath = project, SourceFile = project, Confidence = confidence,
            ConfidenceNote = confidence == Confidences.High ? null : "motivo em texto", ScannerId = "dotnet", ScannerVersion = "1.0.0"
        };

    private static RepositoryIntegration Integration(
        RepositoryAnalysis analysis, string? host, string reference = "IPagamentosServiceCliente", string? environment = null,
        string state = IntegrationStates.Resolved, int? port = null, string? basePath = "/pagamentos") =>
        new()
        {
            Id = Guid.NewGuid().ToString("n"), AnalysisId = analysis.Id, Mechanism = IntegrationMechanisms.Config,
            Scheme = host is null ? null : "https", Host = host, Port = port, BasePath = basePath, Reference = reference,
            ConfigKey = $"{reference}:BaseUrl", Environment = environment,
            EnvironmentKind = environment is null ? null : "prod", State = state, ProjectPath = "src/App",
            SourceFile = environment is null ? "src/App/appsettings.json" : $"src/App/appsettings.{environment}.json",
            Confidence = Confidences.High, ConfidenceNote = null, ScannerId = "dotnet", ScannerVersion = "1.1.0"
        };

    [Fact]
    public async Task Integrations_AreStoredWithTheAnalysis_AndComeBackFieldByField()
    {
        var (_, appId, repoId) = await SeedAsync();
        var analysis = Analysis(repoId, T1);
        var stored = Integration(analysis, "faturas.exemplo.com", "IFaturasServiceCliente", "Production", port: 8443, basePath: "/api");
        var unresolved = Integration(analysis, null, "IEstoqueServiceCliente", state: IntegrationStates.Unresolved, basePath: null) with
        {
            Confidence = Confidences.Low, ConfidenceNote = "valor vazio"
        };
        await _analyses.AddAsync(analysis, [], [stored, unresolved]);

        var view = Assert.Single(await _analyses.GetCurrentIntegrationsByAppAsync(appId));

        Assert.Equal(analysis.Id, view.Current!.Id);
        Assert.Equal(2, view.Integrations.Count);
        Assert.Equal(stored, view.Integrations.Single(i => i.Reference == "IFaturasServiceCliente"));
        var back = view.Integrations.Single(i => i.Reference == "IEstoqueServiceCliente");
        Assert.Null(back.Host);
        Assert.Equal(IntegrationStates.Unresolved, back.State);
        Assert.Equal("valor vazio", back.ConfidenceNote);
    }

    [Fact]
    public async Task Integrations_OnlyTheCurrentAnalysisCounts_AndAFailureDoesNotHideThem()
    {
        var (_, appId, repoId) = await SeedAsync();
        var old = Analysis(repoId, T1);
        var current = Analysis(repoId, T2);
        var failure = Analysis(repoId, T3, AnalysisStatuses.Failed, sha: "x", "sem acesso");
        await _analyses.AddAsync(old, [], [Integration(old, "antigo.exemplo.com")]);
        await _analyses.AddAsync(current, [], [Integration(current, "novo.exemplo.com")]);
        await _analyses.AddAsync(failure, []);

        var view = Assert.Single(await _analyses.GetCurrentIntegrationsByAppAsync(appId));

        Assert.Equal("novo.exemplo.com", Assert.Single(view.Integrations).Host);
        Assert.Equal(failure.Id, view.LastFailure!.Id);
    }

    [Fact]
    public async Task IntegrationRows_CarryTheAppAndTheSquad_FilterByHostIgnoringCase_AndEscapeWildcards()
    {
        var (squadId, appId, repoId) = await SeedAsync();
        var marker = Guid.NewGuid().ToString("n")[..8];
        var analysis = Analysis(repoId, T1);
        await _analyses.AddAsync(analysis, [], [
            Integration(analysis, $"Pagamentos-{marker}.exemplo.com"),
            Integration(analysis, $"outro-{marker}.exemplo.com", "IOutroServiceCliente"),
            Integration(analysis, null, "IFaturasServiceCliente", state: IntegrationStates.Unresolved)   // sem host: fora da lista
        ]);

        var all = await _analyses.ListIntegrationRowsAsync(marker, null);
        var filtered = await _analyses.ListIntegrationRowsAsync($"PAGAMENTOS-{marker}", null);
        var wildcard = await _analyses.ListIntegrationRowsAsync("%", null);
        var otherSquad = await _analyses.ListIntegrationRowsAsync(marker, "sq-que-nao-existe");
        var ofSquad = await _analyses.ListIntegrationRowsAsync(marker, squadId);

        Assert.Equal(2, all.Count);
        Assert.Equal($"pagamentos-{marker}.exemplo.com", Assert.Single(filtered).Integration.Host!.ToLowerInvariant());
        Assert.DoesNotContain(wildcard, r => r.AppId == appId);   // "%" é texto, não curinga
        Assert.Empty(otherSquad);
        Assert.Equal(2, ofSquad.Count);
        var row = filtered[0];
        Assert.Equal(appId, row.AppId);
        Assert.Equal(squadId, row.SquadId);
        Assert.Equal(repoId, row.RepositoryId);
        Assert.Equal(T1, row.AnalyzedAt);
    }

    [Fact]
    public async Task Integrations_AreRemovedWithTheRepository()
    {
        var (_, appId, repoId) = await SeedAsync();
        var marker = Guid.NewGuid().ToString("n")[..8];
        var analysis = Analysis(repoId, T1);
        await _analyses.AddAsync(analysis, [], [Integration(analysis, $"somem-{marker}.exemplo.com")]);
        Assert.Single(await _analyses.ListIntegrationRowsAsync(marker, null));

        await _apps.DeleteRepositoryAsync(appId, repoId);

        Assert.Empty(await _analyses.ListIntegrationRowsAsync(marker, null));
    }

    [Fact]
    public async Task Current_IsTheMostRecentAnalysisThatDidNotFail()
    {
        var (_, _, repoId) = await SeedAsync();
        Assert.Null(await _analyses.GetCurrentAsync(repoId));

        var first = Analysis(repoId, T1);
        var second = Analysis(repoId, T2, AnalysisStatuses.Partial, "sha-novo", "linha 1", "linha 2");
        var failure = Analysis(repoId, T3, AnalysisStatuses.Failed, sha: "x");
        await _analyses.AddAsync(first, []);
        await _analyses.AddAsync(second, []);
        await _analyses.AddAsync(failure, []);

        var current = await _analyses.GetCurrentAsync(repoId);

        Assert.NotNull(current);
        Assert.Equal(second.Id, current.Id);            // a falha mais nova não esconde a análise boa
        Assert.Equal("sha-novo", current.CommitSha);
        Assert.Equal(AnalysisStatuses.Partial, current.Status);
        Assert.Equal(["linha 1", "linha 2"], current.Warnings);
        Assert.Equal(T2, current.AnalyzedAt);
    }

    [Fact]
    public async Task ByApp_ListsEveryRepository_WithItsCurrentFactsOnly_AndAFailureNewerThanTheCurrent()
    {
        var (_, appId, analyzedRepo) = await SeedAsync();
        var neverAnalyzed = $"repo-{Guid.NewGuid():N}";
        var failedAfter = $"repo-{Guid.NewGuid():N}";
        var failedBefore = $"repo-{Guid.NewGuid():N}";
        foreach (var id in new[] { neverAnalyzed, failedAfter, failedBefore })
        {
            await _apps.AddRepositoryAsync(new Repository { Id = id, AppId = appId, Name = $"r-{id[5..9]}", Status = "legacy" });
        }

        var old = Analysis(analyzedRepo, T1);
        var current = Analysis(analyzedRepo, T2);
        await _analyses.AddAsync(old, [Fact(old, "Serilog", "2.0.0")]);
        await _analyses.AddAsync(current, [Fact(current, "Serilog", "3.1.1"), Fact(current, "net8.0", null, FactKinds.Framework)]);

        var good = Analysis(failedAfter, T1);
        await _analyses.AddAsync(good, [Fact(good, "Polly", "8.0.0")]);
        await _analyses.AddAsync(Analysis(failedAfter, T2, AnalysisStatuses.Failed, sha: "x", "sem acesso"), []);

        var failureThenGood = Analysis(failedBefore, T2);
        await _analyses.AddAsync(Analysis(failedBefore, T1, AnalysisStatuses.Failed, sha: "x"), []);
        await _analyses.AddAsync(failureThenGood, []);

        var views = await _analyses.GetCurrentByAppAsync(appId);

        Assert.Equal(4, views.Count);

        var analyzed = views.Single(v => v.Repository.Id == analyzedRepo);
        Assert.Equal(current.Id, analyzed.Current!.Id);
        Assert.Null(analyzed.LastFailure);
        Assert.Equal(["net8.0", "Serilog"], analyzed.Facts.Select(f => f.Name));      // frameworks antes de pacotes; só os da corrente
        Assert.Equal("3.1.1", analyzed.Facts.Single(f => f.Name == "Serilog").Version);

        Assert.Null(views.Single(v => v.Repository.Id == neverAnalyzed).Current);

        var withNewerFailure = views.Single(v => v.Repository.Id == failedAfter);
        Assert.Equal(good.Id, withNewerFailure.Current!.Id);
        Assert.Contains("sem acesso", withNewerFailure.LastFailure!.Warnings);
        Assert.Single(withNewerFailure.Facts);

        var withOlderFailure = views.Single(v => v.Repository.Id == failedBefore);
        Assert.Equal(failureThenGood.Id, withOlderFailure.Current!.Id);
        Assert.Null(withOlderFailure.LastFailure);   // a falha antiga já foi superada

        Assert.Empty(await _analyses.GetCurrentByAppAsync("app-que-nao-existe"));
    }

    [Fact]
    public async Task SearchPackages_IgnoresCase_UsesOnlyTheCurrentAnalysis_AndCarriesAppSquadAndRepository()
    {
        var package = $"Pacote.{Guid.NewGuid():N}";
        var (squadId, appId, repoId) = await SeedAsync(repoStatus: "legacy");
        var (otherSquad, _, otherRepo) = await SeedAsync();

        var old = Analysis(repoId, T1);
        var current = Analysis(repoId, T2);
        await _analyses.AddAsync(old, [Fact(old, package, "1.0.0")]);
        await _analyses.AddAsync(current, [Fact(current, package, "2.0.0", confidence: Confidences.Medium), Fact(current, "net8.0", null, FactKinds.Framework)]);
        var other = Analysis(otherRepo, T1);
        await _analyses.AddAsync(other, [Fact(other, package.ToUpperInvariant(), "3.0.0")]);

        var all = await _analyses.SearchPackagesAsync(package.ToLowerInvariant(), null);

        Assert.Equal(2, all.Count);
        var mine = all.Single(r => r.RepositoryId == repoId);
        Assert.Equal("2.0.0", mine.Fact.Version);                  // a análise antiga não aparece
        Assert.Equal(Confidences.Medium, mine.Fact.Confidence);
        Assert.Equal("motivo em texto", mine.Fact.ConfidenceNote);
        Assert.Equal("legacy", mine.RepositoryStatus);
        Assert.Equal(appId, mine.AppId);
        Assert.Equal(squadId, mine.SquadId);
        Assert.False(string.IsNullOrEmpty(mine.SquadName));
        Assert.Equal(T2, mine.AnalyzedAt);

        var onlyOther = await _analyses.SearchPackagesAsync(package, otherSquad);
        Assert.Equal(otherRepo, Assert.Single(onlyOther).RepositoryId);

        Assert.Empty(await _analyses.SearchPackagesAsync("Pacote.Inexistente", null));
        Assert.DoesNotContain(await _analyses.SearchPackagesAsync(null, squadId), r => r.Fact.Kind == FactKinds.Framework);
    }

    [Fact]
    public async Task FrameworkUsage_ConsidersOnlyActiveRepositories_AndKeepsAppsWithoutInformation()
    {
        var (_, withFramework, activeRepo) = await SeedAsync();
        var (_, legacyOnly, legacyRepo) = await SeedAsync(repoStatus: "legacy");
        var (_, neverAnalyzed, _) = await SeedAsync();
        var (_, inactive, inactiveRepo) = await SeedAsync(appStatus: "inactive");

        foreach (var (repo, framework) in new[] { (activeRepo, "net8.0"), (legacyRepo, "net6.0"), (inactiveRepo, "net8.0") })
        {
            var analysis = Analysis(repo, T1);
            await _analyses.AddAsync(analysis, [Fact(analysis, framework, null, FactKinds.Framework)]);
        }

        var rows = await _analyses.ListFrameworkUsageAsync();

        Assert.Equal("net8.0", Assert.Single(rows, r => r.AppId == withFramework).Framework);
        Assert.Null(Assert.Single(rows, r => r.AppId == legacyOnly).Framework);        // só legacy: sem informação
        Assert.Null(Assert.Single(rows, r => r.AppId == neverAnalyzed).Framework);
        Assert.DoesNotContain(rows, r => r.AppId == inactive);                        // app inativo fica de fora
        Assert.False(string.IsNullOrEmpty(rows.Single(r => r.AppId == withFramework).RepositoryName));
    }

    [Fact]
    public async Task RemovingTheRepository_RemovesItsAnalysesAndFacts()
    {
        var package = $"Pacote.{Guid.NewGuid():N}";
        var (_, appId, repoId) = await SeedAsync();
        var analysis = Analysis(repoId, T1);
        await _analyses.AddAsync(analysis, [Fact(analysis, package)]);
        Assert.Single(await _analyses.SearchPackagesAsync(package, null));

        await _apps.DeleteRepositoryAsync(appId, repoId);

        Assert.Empty(await _analyses.SearchPackagesAsync(package, null));
        Assert.Null(await _analyses.GetCurrentAsync(repoId));
    }

    [Fact]
    public async Task ListRepositoriesBySigla_IgnoresCase_AndCarriesAppAndSquad()
    {
        var sigla = $"t{Guid.NewGuid():N}"[..4];
        var (squadId, appId, repoId) = await SeedAsync(sigla: sigla.ToUpperInvariant());
        await SeedAsync(sigla: "zzz9");

        var found = await _apps.ListRepositoriesBySiglaAsync(sigla);

        var item = Assert.Single(found);
        Assert.Equal(repoId, item.Repository.Id);
        Assert.Equal(appId, item.AppId);
        Assert.Equal(squadId, item.SquadId);
        Assert.Empty(await _apps.ListRepositoriesBySiglaAsync("nada"));
    }
}

/// <summary>Cobertura é global ao banco: precisa de um banco só seu para os números serem exatos.</summary>
public class AnalysisCoverageTests(SqliteFixture fixture) : IClassFixture<SqliteFixture>
{
    [Fact]
    public async Task Coverage_CountsRepositoriesWithoutAGoodAnalysis_AndReportsTheLatestDate()
    {
        var squads = new SquadRepository(fixture.ConnectionFactory);
        var apps = new AppRepository(fixture.ConnectionFactory);
        var analyses = new AnalysisRepository(fixture.ConnectionFactory, TestAnalysisWriters.All);

        Assert.Equal(new AnalysisCoverage(0, null), await analyses.GetCoverageAsync());

        await squads.SaveAsync(new Squad { Id = "sq-1", Name = "Squad" });
        await apps.SaveAsync(new App { Id = "app-1", SquadId = "sq-1", Name = "App" });
        foreach (var id in new[] { "r-ok", "r-failed", "r-never" })
        {
            await apps.AddRepositoryAsync(new Repository { Id = id, AppId = "app-1", Name = id, Status = "active" });
        }

        var at = new DateTimeOffset(2026, 10, 3, 9, 30, 0, TimeSpan.Zero);
        await analyses.AddAsync(new RepositoryAnalysis { Id = "a1", RepositoryId = "r-ok", AnalyzedAt = at.AddDays(-1), Status = AnalysisStatuses.Ok }, []);
        await analyses.AddAsync(new RepositoryAnalysis { Id = "a2", RepositoryId = "r-ok", AnalyzedAt = at, Status = AnalysisStatuses.NoScanner }, []);
        await analyses.AddAsync(new RepositoryAnalysis { Id = "a3", RepositoryId = "r-failed", AnalyzedAt = at.AddDays(5), Status = AnalysisStatuses.Failed }, []);

        var coverage = await analyses.GetCoverageAsync();

        Assert.Equal(2, coverage.UnanalyzedRepositories);          // r-failed (só falhou) e r-never
        Assert.Equal(at, coverage.LatestAnalyzedAt);               // a falha não conta como análise
    }
}
