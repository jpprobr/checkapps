using CheckApps.Application.Analysis;
using CheckApps.Domain;
using CheckApps.Domain.Analysis;
using CheckApps.Infrastructure.Sqlite;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.IntegrationTests.Sqlite;

/// <summary>
/// As leituras da spec 013 (app, busca de pacote e panorama de framework) com o serviço e o SQLite de verdade juntos.
/// Banco só desta classe: as consultas globais (panorama, cobertura) precisam de números exatos.
/// </summary>
public class DependencyServiceTests(SqliteFixture fixture) : IClassFixture<SqliteFixture>
{
    private static readonly DateTimeOffset T1 = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SquadRepository _squads = new(fixture.ConnectionFactory);
    private readonly AppRepository _apps = new(fixture.ConnectionFactory);
    private readonly AnalysisRepository _analyses = new(fixture.ConnectionFactory);

    private DependencyService Sut(params string[] internalPrefixes) =>
        new(_apps, _analyses, new AnalysisOptions { InternalPackagePrefixes = internalPrefixes });

    private async Task<(string AppId, string RepoId)> SeedAppAsync(string squadId, string appName, string repoStatus = "active")
    {
        var appId = $"app-{Guid.NewGuid():N}";
        var repoId = $"repo-{Guid.NewGuid():N}";
        await _apps.SaveAsync(new App { Id = appId, SquadId = squadId, Name = appName });
        await _apps.AddRepositoryAsync(new Repository { Id = repoId, AppId = appId, Name = $"{appName}-repo", Status = repoStatus });
        return (appId, repoId);
    }

    private async Task<string> SeedSquadAsync(string name)
    {
        var id = $"sq-{Guid.NewGuid():N}";
        await _squads.SaveAsync(new Squad { Id = id, Name = name });
        return id;
    }

    private async Task AnalyzeAsync(
        string repoId, DateTimeOffset at, params (string Kind, string Name, string? Version, string Project)[] facts)
    {
        var analysis = new RepositoryAnalysis
        {
            Id = Guid.NewGuid().ToString("n"), RepositoryId = repoId, AnalyzedAt = at, CommitSha = $"sha-{at.Ticks}",
            Status = AnalysisStatuses.Ok
        };
        await _analyses.AddAsync(analysis, facts.Select(f => new RepositoryDependency
        {
            Id = Guid.NewGuid().ToString("n"), AnalysisId = analysis.Id,
            Ecosystem = f.Kind == FactKinds.Framework ? Ecosystems.DotNet : Ecosystems.NuGet, Kind = f.Kind, Name = f.Name,
            Version = f.Version, ProjectPath = f.Project, SourceFile = f.Project, Confidence = Confidences.High,
            ScannerId = "dotnet", ScannerVersion = "1.0.0"
        }).ToList());
    }

    // ---- Um app ----------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task ForApp_SplitsFrameworksAndPackages_FlagsInternalOnes_AndKeepsRepositoriesWithoutAnalysis()
    {
        var squad = await SeedSquadAsync("Squad A");
        var (appId, repoId) = await SeedAppAsync(squad, "Pagamentos");
        await _apps.AddRepositoryAsync(new Repository { Id = $"repo-{Guid.NewGuid():N}", AppId = appId, Name = "legado", Status = "legacy" });
        await AnalyzeAsync(repoId, T1,
            (FactKinds.Framework, "net8.0", null, "src/Api/Api.csproj"),
            (FactKinds.Package, "Serilog", "3.1.1", "src/Api/Api.csproj"),
            (FactKinds.Package, "Empresa.SDK.TokenManager", "2.0.0", "src/Api/Api.csproj"));

        var result = await Sut("Empresa.").GetForAppAsync(appId);

        Assert.True(result.InternalPrefixesConfigured);
        Assert.Equal(2, result.Repositories.Count);

        var analyzed = result.Repositories.Single(r => r.Repository.Id == repoId);
        Assert.NotNull(analyzed.Analysis);
        Assert.Equal(["net8.0"], analyzed.Frameworks.Select(f => f.Fact.Name));
        Assert.Equal(["Empresa.SDK.TokenManager", "Serilog"], analyzed.Packages.Select(f => f.Fact.Name));
        Assert.True(analyzed.Packages.Single(p => p.Fact.Name == "Empresa.SDK.TokenManager").IsInternal);
        Assert.False(analyzed.Packages.Single(p => p.Fact.Name == "Serilog").IsInternal);

        var never = result.Repositories.Single(r => r.Repository.Name == "legado");
        Assert.Null(never.Analysis);
        Assert.Empty(never.Packages);
    }

    [Fact]
    public async Task ForApp_WithoutAConfiguredPrefix_MarksNothingAsInternal()
    {
        var squad = await SeedSquadAsync("Squad A");
        var (appId, repoId) = await SeedAppAsync(squad, "App");
        await AnalyzeAsync(repoId, T1, (FactKinds.Package, "Empresa.SDK.TokenManager", "2.0.0", "A.csproj"));

        var result = await Sut().GetForAppAsync(appId);

        Assert.False(result.InternalPrefixesConfigured);
        Assert.False(result.Repositories.Single().Packages.Single().IsInternal);
    }

    [Fact]
    public async Task ForApp_ThrowsForAnUnknownApp() =>
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().GetForAppAsync("nao-existe"));

    // ---- Busca de pacote -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Search_SummarizesVersionsByApp_CountingAnAppOnce_AndFiltersBySquad()
    {
        var package = $"Pacote.{Guid.NewGuid():N}";
        var squadA = await SeedSquadAsync("Squad A");
        var squadB = await SeedSquadAsync("Squad B");
        var (_, repoA1) = await SeedAppAsync(squadA, "A1");
        var (_, repoA2) = await SeedAppAsync(squadA, "A2");
        var (_, repoB) = await SeedAppAsync(squadB, "B1");
        await AnalyzeAsync(repoA1, T1, (FactKinds.Package, package, "1.0.0", "a/A.csproj"), (FactKinds.Package, package, "1.0.0", "b/B.csproj"));
        await AnalyzeAsync(repoA2, T1, (FactKinds.Package, package.ToUpperInvariant(), "2.0.0", "A.csproj"));
        await AnalyzeAsync(repoB, T1, (FactKinds.Package, package, "1.0.0", "A.csproj"));

        var all = await Sut().SearchPackagesAsync(package.ToLowerInvariant(), null, internalOnly: false);

        Assert.Equal(4, all.Usages.Count);                                                  // uma linha por projeto
        Assert.Equal(
            [("1.0.0", 2), ("2.0.0", 1)],                                                   // A1 conta uma vez, mesmo com dois projetos
            all.Versions.Select(v => (v.Version!, v.AppCount)));

        var onlyB = await Sut().SearchPackagesAsync(package, squadB, internalOnly: false);
        Assert.Single(onlyB.Usages);
        Assert.Equal("Squad B", onlyB.Usages.Single().Row.SquadName);

        var none = await Sut().SearchPackagesAsync("Pacote.NaoExiste", null, internalOnly: false);
        Assert.Empty(none.Usages);
        Assert.Empty(none.Versions);
    }

    [Fact]
    public async Task Search_RequiresAName_UnlessAskingForInternalPackages()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().SearchPackagesAsync(null, null, internalOnly: false));
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().SearchPackagesAsync("  ", null, internalOnly: false));
    }

    [Fact]
    public async Task Search_InternalOnly_WithoutAName_ListsOnlyInternalPackages_AndExplainsWhenThereIsNoPrefix()
    {
        var token = $"Empresa.SDK.Token{Guid.NewGuid():N}";
        var squad = await SeedSquadAsync("Squad A");
        var (_, repo) = await SeedAppAsync(squad, "App");
        await AnalyzeAsync(repo, T1, (FactKinds.Package, token, "2.0.0", "A.csproj"), (FactKinds.Package, "Serilog", "3.1.1", "A.csproj"));

        var internals = await Sut("Empresa.").SearchPackagesAsync(null, null, internalOnly: true);
        var noPrefix = await Sut().SearchPackagesAsync(null, null, internalOnly: true);

        Assert.Contains(internals.Usages, u => u.Row.Fact.Name == token && u.IsInternal);
        Assert.DoesNotContain(internals.Usages, u => u.Row.Fact.Name == "Serilog");
        Assert.True(internals.InternalPrefixesConfigured);

        Assert.Empty(noPrefix.Usages);
        Assert.False(noPrefix.InternalPrefixesConfigured);
    }

    [Fact]
    public async Task Search_ReportsHowManyRepositoriesHaveNeverBeenAnalyzed_SoTheAnswerIsNotTakenAsComplete()
    {
        var squad = await SeedSquadAsync("Squad A");
        var package = $"Pacote.{Guid.NewGuid():N}";
        var (_, analyzed) = await SeedAppAsync(squad, "Analisado");
        await AnalyzeAsync(analyzed, T1.AddDays(3), (FactKinds.Package, package, "1.0.0", "A.csproj"));
        var before = (await Sut().SearchPackagesAsync(package, null, internalOnly: false)).Coverage;
        await SeedAppAsync(squad, "Nunca analisado");

        var after = (await Sut().SearchPackagesAsync(package, null, internalOnly: false)).Coverage;

        Assert.Equal(before.UnanalyzedRepositories + 1, after.UnanalyzedRepositories);
        Assert.True(after.LatestAnalyzedAt >= T1.AddDays(3));
    }

    // ---- Panorama de framework ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task Frameworks_GroupsByFrameworkAndSquad_CountsAnAppInEachOfItsFrameworks_AndKeepsTheUnknownOnes()
    {
        var squadA = await SeedSquadAsync("Squad Alfa");
        var squadB = await SeedSquadAsync("Squad Beta");
        var (appNet8, repo8) = await SeedAppAsync(squadA, "Em net8");
        var (appBoth, repoBoth) = await SeedAppAsync(squadA, "Em dois");
        var (appUnknown, _) = await SeedAppAsync(squadB, "Sem informação");
        var (appLegacyOnly, legacyRepo) = await SeedAppAsync(squadB, "Só legado", repoStatus: "legacy");
        await AnalyzeAsync(repo8, T1, (FactKinds.Framework, "net8.0", null, "A.csproj"));
        await AnalyzeAsync(repoBoth, T1, (FactKinds.Framework, "net6.0", null, "A.csproj"), (FactKinds.Framework, "net8.0", null, "B.csproj"));
        await AnalyzeAsync(legacyRepo, T1, (FactKinds.Framework, "net48", null, "A.csproj"));

        var overview = await Sut().GetFrameworkOverviewAsync();

        FrameworkCell Cell(string? framework, string squad) =>
            overview.Cells.Single(c => c.Framework == framework && c.SquadId == squad);

        Assert.Equal(new[] { appBoth, appNet8 }.Order(), Cell("net8.0", squadA).Apps.Select(a => a.AppId).Order());
        Assert.Equal([appBoth], Cell("net6.0", squadA).Apps.Select(a => a.AppId));
        Assert.Equal(["Em dois-repo"], Cell("net6.0", squadA).Apps.Single().Repositories);

        // Repositório legacy não conta; o app dele cai em "sem informação" junto com o nunca analisado.
        Assert.Equal(new[] { appLegacyOnly, appUnknown }.Order(), Cell(null, squadB).Apps.Select(a => a.AppId).Order());
        Assert.DoesNotContain(overview.Cells, c => c.Framework == "net48");

        // "Sem informação" vem por último.
        Assert.Null(overview.Cells[^1].Framework);
    }
}
