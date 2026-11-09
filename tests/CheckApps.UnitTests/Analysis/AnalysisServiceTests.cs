using CheckApps.Application.Analysis;
using CheckApps.Application.Discovery;
using CheckApps.Domain;
using CheckApps.Domain.Analysis;
using CheckApps.Infrastructure.Analysis.DotNet;
using CheckApps.UnitTests.TestDoubles;

namespace CheckApps.UnitTests.Analysis;

/// <summary>
/// O serviço de análise com fonte, repositório de análises e relógio falsos — sem rede nem banco. Os scanners são os
/// reais; o conteúdo dos arquivos é mínimo e escrito no teste.
/// </summary>
public class AnalysisServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    private const string Csproj =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
          <ItemGroup><PackageReference Include="Serilog" Version="3.1.1" /></ItemGroup>
        </Project>
        """;

    private readonly FakeAppRepository _apps = new();
    private readonly FakeAnalysisRepository _analyses = new();
    private readonly FakeContentSource _source = new();
    private readonly FixedTimeProvider _clock = new(T0);
    private readonly AnalysisOptions _options = new();
    private readonly List<IRepositoryScanner> _scanners = [new DotNetScanner()];

    private AnalysisService Sut() =>
        new(_source, new ScannerRegistry(_scanners), Processors(), _apps, _analyses, _options, new ScanOptions(), _clock);

    private IAnalysisFactProcessor[] Processors() => [new DependencyFactProcessor(), new IntegrationFactProcessor(_options)];

    private async Task<string> AddRepositoryAsync(string id, string sigla = "ab1", string provider = "fake")
    {
        _apps.Add(new App { Id = $"app-{id}", SquadId = "sq-1", Name = $"App {id}" });
        await _apps.AddRepositoryAsync(new Repository
        {
            Id = id, AppId = $"app-{id}", Name = $"repo-{id}", Provider = provider, ExternalId = id, Sigla = sigla, Status = "active"
        });
        return id;
    }

    private static Dictionary<string, string> Files(params (string Path, string Content)[] files) =>
        files.ToDictionary(f => f.Path, f => f.Content);

    // ---- US1: analisar uma sigla ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Analyze_StoresOneAnalysisPerCatalogRepository_WithFactsThatSayWhereTheyCameFrom()
    {
        await AddRepositoryAsync("r1");
        await AddRepositoryAsync("r2");
        await AddRepositoryAsync("outra", sigla: "cd2");   // outra sigla: não entra
        _source.With("r1", "sha-1", Files(("src/App/App.csproj", Csproj)))
               .With("r2", "sha-2", Files(("README.md", "# nada")))
               .With("outra", "sha-3", Files(("src/App/App.csproj", Csproj)));

        var report = await Sut().AnalyzeAsync(["AB1"], dryRun: false);   // a sigla vem em qualquer caixa

        Assert.Equal(2, report.Items.Count);
        Assert.Equal(2, report.Count(AnalysisOutcome.Analyzed));
        Assert.Equal(1, report.CountStatus(AnalysisStatuses.Ok));
        Assert.Equal(1, report.CountStatus(AnalysisStatuses.NoScanner));

        var withScanner = _analyses.Analyses.Single(a => a.RepositoryId == "r1");
        Assert.Equal(AnalysisStatuses.Ok, withScanner.Status);
        Assert.Equal("sha-1", withScanner.CommitSha);
        Assert.Equal(T0, withScanner.AnalyzedAt);
        Assert.Equal("GITHUB_SCAN", withScanner.Source);

        var facts = _analyses.Facts.Where(f => f.AnalysisId == withScanner.Id).ToList();
        Assert.Equal(["net8.0", "Serilog"], facts.OrderBy(f => f.Kind).ThenBy(f => f.Name).Select(f => f.Name));
        Assert.All(facts, f =>
        {
            Assert.Equal("PARSER", f.Origin);
            Assert.Equal("dotnet", f.ScannerId);
            Assert.False(string.IsNullOrEmpty(f.ScannerVersion));
            Assert.Equal("src/App/App.csproj", f.ProjectPath);
        });
        Assert.Equal(facts.Count, facts.Select(f => f.Id).Distinct().Count());

        var withoutScanner = _analyses.Analyses.Single(a => a.RepositoryId == "r2");
        Assert.Equal(AnalysisStatuses.NoScanner, withoutScanner.Status);
        Assert.DoesNotContain(_analyses.Facts, f => f.AnalysisId == withoutScanner.Id);
        Assert.DoesNotContain("outra", _analyses.Analyses.Select(a => a.RepositoryId));
    }

    [Fact]
    public async Task Analyze_SameCommit_CreatesNothingNew_AndReadsNoFile()
    {
        await AddRepositoryAsync("r1");
        _source.With("r1", "sha-1", Files(("App.csproj", Csproj)));

        await Sut().AnalyzeAsync(["ab1"], dryRun: false);
        var readsAfterFirst = _source.ReadCalls;
        var second = await Sut().AnalyzeAsync(["ab1"], dryRun: false);

        Assert.Equal(1, second.Count(AnalysisOutcome.Unchanged));
        Assert.Equal(0, second.Count(AnalysisOutcome.Analyzed));
        Assert.Single(_analyses.Analyses);
        Assert.Equal(readsAfterFirst, _source.ReadCalls);   // nenhum arquivo lido de novo
    }

    [Fact]
    public async Task Analyze_NewCommit_CreatesANewAnalysis_AndKeepsTheOldOneAsHistory()
    {
        await AddRepositoryAsync("r1");
        _source.With("r1", "sha-1", Files(("App.csproj", Csproj)));
        await Sut().AnalyzeAsync(["ab1"], dryRun: false);

        _clock.Advance(TimeSpan.FromDays(1));
        _source.With("r1", "sha-2", Files(("App.csproj", Csproj.Replace("3.1.1", "4.0.0"))));
        var report = await Sut().AnalyzeAsync(["ab1"], dryRun: false);

        Assert.Equal(1, report.Count(AnalysisOutcome.Analyzed));
        Assert.Equal(["sha-1", "sha-2"], _analyses.Analyses.OrderBy(a => a.AnalyzedAt).Select(a => a.CommitSha));
        Assert.Contains(_analyses.Facts, f => f.Name == "Serilog" && f.Version == "3.1.1");
        Assert.Contains(_analyses.Facts, f => f.Name == "Serilog" && f.Version == "4.0.0");
    }

    [Fact]
    public async Task Analyze_DryRun_ReportsWhatWouldBeStored_ButStoresNothing()
    {
        await AddRepositoryAsync("r1");
        await AddRepositoryAsync("r2");
        _source.With("r1", "sha-1", Files(("App.csproj", Csproj))).Failing("r2", "sem acesso");

        var report = await Sut().AnalyzeAsync(["ab1"], dryRun: true);

        Assert.True(report.DryRun);
        Assert.Equal(2, report.Items.Single(i => i.Catalog.Repository.Id == "r1").FactCount);
        Assert.Equal(AnalysisOutcome.Failed, report.Items.Single(i => i.Catalog.Repository.Id == "r2").Outcome);
        Assert.Empty(_analyses.Analyses);   // nem a análise, nem a falha
    }

    // ---- Situações ------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Analyze_AWarningMakesTheAnalysisPartial_AndNeverCarriesFileContent()
    {
        await AddRepositoryAsync("r1");
        _source.With("r1", "sha-1", Files(
            ("Bom/Bom.csproj", Csproj),
            ("Quebrado/Quebrado.csproj", "isto-e-conteudo-secreto <Project")));

        var report = await Sut().AnalyzeAsync(["ab1"], dryRun: false);

        var item = Assert.Single(report.Items);
        Assert.Equal(AnalysisStatuses.Partial, item.Status);
        var warning = Assert.Single(item.Warnings);
        Assert.Contains("Quebrado/Quebrado.csproj", warning);
        Assert.DoesNotContain("conteudo-secreto", warning);
        Assert.Contains(_analyses.Facts, f => f.ProjectPath == "Bom/Bom.csproj");   // o bom continua analisado
        Assert.DoesNotContain("conteudo-secreto", string.Join('\n', _analyses.Analyses.Single().Warnings));
    }

    [Fact]
    public async Task Analyze_ATruncatedTree_MakesTheAnalysisPartial()
    {
        await AddRepositoryAsync("r1");
        _source.With("r1", "sha-1", Files(("App.csproj", Csproj)), truncated: true);

        var item = Assert.Single((await Sut().AnalyzeAsync(["ab1"], dryRun: false)).Items);

        Assert.Equal(AnalysisStatuses.Partial, item.Status);
        Assert.Contains(item.Warnings, w => w.Contains("truncada"));
    }

    [Fact]
    public async Task Analyze_ARepositoryWithoutAnApplicableScanner_ButATruncatedTree_IsPartialNotNoScanner()
    {
        await AddRepositoryAsync("r1");
        _source.With("r1", "sha-1", Files(("README.md", "x")), truncated: true);

        Assert.Equal(AnalysisStatuses.Partial, Assert.Single((await Sut().AnalyzeAsync(["ab1"], dryRun: false)).Items).Status);
    }

    [Fact]
    public async Task Analyze_RespectsTheProjectLimit_ReadingOnlyTheFirstFiles()
    {
        _options.MaxProjectFiles = 1;
        await AddRepositoryAsync("r1");
        _source.With("r1", "sha-1", Files(("a/A.csproj", Csproj), ("b/B.csproj", Csproj)));

        var item = Assert.Single((await Sut().AnalyzeAsync(["ab1"], dryRun: false)).Items);

        Assert.Equal(AnalysisStatuses.Partial, item.Status);
        Assert.Equal(["a/A.csproj"], _source.ReadPaths);
        Assert.Contains(item.Warnings, w => w.Contains("só os primeiros 1"));
        Assert.All(_analyses.Facts, f => Assert.Equal("a/A.csproj", f.ProjectPath));
    }

    [Fact]
    public async Task Analyze_SkipsFilesOverTheSizeLimit_WithoutReadingThem()
    {
        _options.MaxFileBytes = 50;
        await AddRepositoryAsync("r1");
        _source.With("r1", "sha-1", Files(("Grande/Grande.csproj", Csproj), ("Pequeno/Pequeno.csproj", "<Project />")));

        var item = Assert.Single((await Sut().AnalyzeAsync(["ab1"], dryRun: false)).Items);

        Assert.Equal(AnalysisStatuses.Partial, item.Status);
        Assert.Equal(["Pequeno/Pequeno.csproj"], _source.ReadPaths);
        Assert.Contains(item.Warnings, w => w.Contains("Grande/Grande.csproj") && w.Contains("limite"));
    }

    [Fact]
    public async Task Analyze_AFileThatVanishedBetweenListingAndReading_IsAWarning_NotAFailure()
    {
        await AddRepositoryAsync("r1");
        _source.With("r1", "sha-1", Files(("App.csproj", Csproj))).Vanishing("App.csproj");

        var item = Assert.Single((await Sut().AnalyzeAsync(["ab1"], dryRun: false)).Items);

        Assert.Equal(AnalysisOutcome.Analyzed, item.Outcome);
        Assert.Equal(AnalysisStatuses.Partial, item.Status);
        Assert.Contains(item.Warnings, w => w.Contains("não foi encontrado"));
    }

    [Fact]
    public async Task Analyze_AScannerThatThrows_BecomesAWarning_AndTheOthersStillRun()
    {
        _scanners.Add(new ThrowingScanner());
        _scanners.Add(new PackageJsonTestScanner());
        await AddRepositoryAsync("r1");
        _source.With("r1", "sha-1", Files(
            ("App.csproj", Csproj), ("package.json", """{"dependencies":{"react":"18.3.1"}}""")));

        var item = Assert.Single((await Sut().AnalyzeAsync(["ab1"], dryRun: false)).Items);

        Assert.Equal(AnalysisStatuses.Partial, item.Status);
        Assert.Contains(item.Warnings, w => w.Contains("quebrado") && w.Contains("InvalidOperationException"));
        Assert.DoesNotContain("senha", string.Join(' ', item.Warnings));   // só o tipo da exceção, nunca a mensagem
        Assert.Equal(["dotnet", "npm-test"], _analyses.Facts.Select(f => f.ScannerId).Distinct().Order());
    }

    // ---- Falhas e isolamento --------------------------------------------------------------------------------------------

    [Fact]
    public async Task Analyze_AFailureInOneRepository_DoesNotStopTheOthers_AndIsRecordedInTheHistory()
    {
        await AddRepositoryAsync("r1");
        await AddRepositoryAsync("r2");
        _source.Failing("r1", "O GitHub negou o acesso.").With("r2", "sha-2", Files(("App.csproj", Csproj)));

        var report = await Sut().AnalyzeAsync(["ab1"], dryRun: false);

        Assert.Equal(1, report.Count(AnalysisOutcome.Failed));
        Assert.Equal(1, report.Count(AnalysisOutcome.Analyzed));
        var failure = _analyses.Analyses.Single(a => a.RepositoryId == "r1");
        Assert.Equal(AnalysisStatuses.Failed, failure.Status);
        Assert.Null(failure.CommitSha);
        Assert.Equal(["O GitHub negou o acesso."], failure.Warnings);
        Assert.Equal(AnalysisStatuses.Ok, _analyses.Analyses.Single(a => a.RepositoryId == "r2").Status);
    }

    [Fact]
    public async Task Analyze_AFailureDoesNotHideTheLastGoodAnalysis_SoTheSameCommitIsStillUnchanged()
    {
        await AddRepositoryAsync("r1");
        _source.With("r1", "sha-1", Files(("App.csproj", Csproj)));
        await Sut().AnalyzeAsync(["ab1"], dryRun: false);

        _clock.Advance(TimeSpan.FromHours(1));
        var failing = new FakeContentSource().Failing("r1", "limite de chamadas");
        var failed = await new AnalysisService(
            failing, new ScannerRegistry(_scanners), Processors(), _apps, _analyses, _options, new ScanOptions(), _clock)
            .AnalyzeAsync(["ab1"], dryRun: false);
        Assert.Equal(1, failed.Count(AnalysisOutcome.Failed));

        _clock.Advance(TimeSpan.FromHours(1));
        var again = await Sut().AnalyzeAsync(["ab1"], dryRun: false);

        Assert.Equal(1, again.Count(AnalysisOutcome.Unchanged));   // a corrente continua sendo a boa
        Assert.Equal(2, _analyses.Analyses.Count);                  // a boa e a falha; nenhuma nova
    }

    [Fact]
    public async Task Analyze_SkipsRepositoriesTheSourceCannotRead_WithoutListingThem()
    {
        await AddRepositoryAsync("r1", provider: "github");
        _source.With("r1", "sha-1", Files(("App.csproj", Csproj)));

        var report = await Sut().AnalyzeAsync(["ab1"], dryRun: false);

        Assert.Equal(1, report.Count(AnalysisOutcome.Skipped));
        Assert.Equal(0, _source.ListCalls);
        Assert.Empty(_analyses.Analyses);
    }

    // ---- Escopo e configuração ------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("abcd")]
    [InlineData("a_1")]
    public async Task Analyze_RefusesAnEmptyOrMalformedScope_BeforeTouchingTheSource(string? sigla)
    {
        await AddRepositoryAsync("r1");

        await Assert.ThrowsAsync<ArgumentException>(() => Sut().AnalyzeAsync(sigla is null ? null : [sigla], dryRun: false));

        Assert.Equal(0, _source.ListCalls);
    }

    [Fact]
    public async Task Analyze_RefusesToRun_WhenTheSourceIsNotConfigured_WithoutTheValues()
    {
        _source.ConfigurationProblem = "Configure GitHub:Token (ex.: dotnet user-secrets set ...).";
        await AddRepositoryAsync("r1");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Sut().AnalyzeAsync(["ab1"], dryRun: false));

        Assert.Contains("GitHub:Token", ex.Message);
        Assert.Equal(0, _source.ListCalls);
    }

    [Fact]
    public async Task Analyze_ASigla_WithNoRepositories_YieldsAnEmptyReport()
    {
        var report = await Sut().AnalyzeAsync(["zzz"], dryRun: false);

        Assert.Empty(report.Items);
    }

    // ---- Spec 014: integrações declaradas e --force ---------------------------------------------------------------------

    private const string Settings =
        """
        {
          "IPagamentosServiceCliente": { "BaseUrl": "http://empresa.{env}.exemplo.com/pagamentos" },
          "INotificacoesServiceCliente": { "BaseUrl": "https://usuario:SEGREDO-PLANTADO-3@notificacoes.exemplo.com/v1/?key=SEGREDO-PLANTADO-4" }
        }
        """;

    [Fact]
    public async Task Analyze_StoresTheIntegrations_InTheSameAnalysisAsThePackages_AlreadyNormalized()
    {
        await AddRepositoryAsync("r1");
        _source.With("r1", "sha-1", Files(
            ("src/App/App.csproj", Csproj),
            ("src/App/appsettings.json", Settings),
            ("src/App/appsettings.Production.json", """{ "IPagamentosServiceCliente": { "BaseUrl": "https://empresa.exemplo.com/pagamentos" } }""")));

        var item = Assert.Single((await Sut().AnalyzeAsync(["ab1"], dryRun: false)).Items);

        var analysis = Assert.Single(_analyses.Analyses);
        Assert.Equal(3, item.IntegrationCount);
        Assert.Equal(2, item.FactCount);                       // framework + Serilog continuam
        Assert.Equal(3, _analyses.Integrations.Count);
        Assert.All(_analyses.Integrations, i =>
        {
            Assert.Equal(analysis.Id, i.AnalysisId);
            Assert.Equal("dotnet", i.ScannerId);
            Assert.Equal("1.1.0", i.ScannerVersion);
        });
        Assert.Contains(_analyses.Integrations, i => i.Host == "notificacoes.exemplo.com" && i.BasePath == "/v1");
        Assert.Contains(_analyses.Integrations, i => i.Environment == "Production" && i.EnvironmentKind == "prod");
    }

    [Fact]
    public async Task Analyze_NeverLetsASecretReachTheStoredData_NorTheReport()
    {
        await AddRepositoryAsync("r1");
        _source.With("r1", "sha-1", Files(("src/App/App.csproj", Csproj), ("src/App/appsettings.json", Settings)));

        var report = await Sut().AnalyzeAsync(["ab1"], dryRun: false);

        var everything = System.Text.Json.JsonSerializer.Serialize(new { _analyses.Analyses, _analyses.Facts, _analyses.Integrations, report.Items });
        Assert.DoesNotContain("SEGREDO-PLANTADO", everything);
        Assert.DoesNotContain("usuario", everything);
    }

    [Fact]
    public async Task Analyze_AnInformativeNote_DoesNotMakeTheAnalysisPartial_ButAWarningDoes()
    {
        await AddRepositoryAsync("r1");
        await AddRepositoryAsync("r2");
        _source.With("r1", "sha-1", Files(
                ("App.csproj", Csproj),
                ("appsettings.json", """{ "S": { "BaseUrl": "http://localhost:5001" } }""")))
               .With("r2", "sha-2", Files(
                ("App.csproj", Csproj),
                ("appsettings.json", """{ "S": { "BaseUrl": "${S_URL}" } }""")));

        var report = await Sut().AnalyzeAsync(["ab1"], dryRun: false);

        var withNote = report.Items.Single(i => i.Catalog.Repository.Id == "r1");
        Assert.Equal(AnalysisStatuses.Ok, withNote.Status);
        Assert.Contains(withNote.Warnings, w => w.StartsWith(AnalysisService.NotePrefix, StringComparison.Ordinal) && w.Contains("1 valor ignorado"));
        Assert.Equal(0, withNote.IntegrationCount);

        var withWarning = report.Items.Single(i => i.Catalog.Repository.Id == "r2");
        Assert.Equal(AnalysisStatuses.Partial, withWarning.Status);
        Assert.Contains(withWarning.Warnings, w => w.Contains("integração não resolvida"));
        Assert.Equal(1, withWarning.IntegrationCount);   // a não resolvida é registrada, sem host
    }

    [Fact]
    public async Task Analyze_InvalidSettingsJson_IsAWarning_AndTheOtherFilesStillCount()
    {
        await AddRepositoryAsync("r1");
        _source.With("r1", "sha-1", Files(
            ("App.csproj", Csproj),
            ("appsettings.json", "{ \"S\": { \"BaseUrl\": \"https://a.exemplo.com\" "),
            ("appsettings.Production.json", """{ "S": { "BaseUrl": "https://b.exemplo.com" } }""")));

        var item = Assert.Single((await Sut().AnalyzeAsync(["ab1"], dryRun: false)).Items);

        Assert.Equal(AnalysisStatuses.Partial, item.Status);
        Assert.Equal(1, item.IntegrationCount);
        Assert.DoesNotContain(item.Warnings, w => w.Contains("a.exemplo.com"));
    }

    [Fact]
    public async Task Analyze_Force_ReanalyzesTheSameCommit_AndKeepsTheOldOneAsHistory()
    {
        await AddRepositoryAsync("r1");
        _source.With("r1", "sha-1", Files(("App.csproj", Csproj), ("appsettings.json", Settings)));
        await Sut().AnalyzeAsync(["ab1"], dryRun: false);

        _clock.Advance(TimeSpan.FromMinutes(5));
        var withoutForce = await Sut().AnalyzeAsync(["ab1"], dryRun: false);
        var withForce = await Sut().AnalyzeAsync(["ab1"], dryRun: false, force: true);

        Assert.Equal(1, withoutForce.Count(AnalysisOutcome.Unchanged));
        Assert.Equal(1, withForce.Count(AnalysisOutcome.Analyzed));
        Assert.Equal(2, _analyses.Analyses.Count);
        Assert.All(_analyses.Analyses, a => Assert.Equal("sha-1", a.CommitSha));
    }

    [Fact]
    public async Task Analyze_ForceWithDryRun_StillStoresNothing()
    {
        await AddRepositoryAsync("r1");
        _source.With("r1", "sha-1", Files(("App.csproj", Csproj), ("appsettings.json", Settings)));
        await Sut().AnalyzeAsync(["ab1"], dryRun: false);

        var report = await Sut().AnalyzeAsync(["ab1"], dryRun: true, force: true);

        Assert.Equal(1, report.Count(AnalysisOutcome.Analyzed));
        Assert.Single(_analyses.Analyses);
        Assert.Equal(2, _analyses.Integrations.Count);   // só os da primeira gravação
    }

    [Fact]
    public async Task Analyze_ReadsTheSettingsFiles_ButNeverSourceCode()
    {
        await AddRepositoryAsync("r1");
        _source.With("r1", "sha-1", Files(
            ("src/App/App.csproj", Csproj),
            ("src/App/appsettings.json", Settings),
            ("src/App/Clients/IPagamentosServiceCliente.cs", "public interface IPagamentosServiceCliente {}")));

        await Sut().AnalyzeAsync(["ab1"], dryRun: false);

        Assert.Contains("src/App/appsettings.json", _source.ReadPaths);
        Assert.DoesNotContain(_source.ReadPaths, p => p.EndsWith(".cs"));
    }

    private sealed class ThrowingScanner : IRepositoryScanner
    {
        public string Id => "quebrado";

        public string Version => "0.0.1";

        public bool AppliesTo(IReadOnlyList<string> paths) => true;

        public IReadOnlyList<string> SelectFiles(IReadOnlyList<string> paths) => [];

        public ScannerResult Analyze(IReadOnlyDictionary<string, string> files) =>
            throw new InvalidOperationException("a senha é 123");
    }
}
