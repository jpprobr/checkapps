using CheckApps.Application.Discovery;
using CheckApps.Application.Persistence;
using CheckApps.Domain.Discovery;
using CheckApps.UnitTests.TestDoubles;

namespace CheckApps.UnitTests.Discovery;

public class ScanServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly StubDiscoverySource _source = new();
    private readonly FakeAppRepository _apps = new();
    private readonly FakeDiscoveredRepositoryRepository _discovered;
    private readonly FixedTimeProvider _clock = new(T0);
    private readonly ScanOptions _options = new() { NameTemplate = "lab-{sigla}-" };

    public ScanServiceTests()
    {
        _discovered = new FakeDiscoveredRepositoryRepository(_apps);
    }

    private ScanService Sut() => new(_source, _discovered, _options, _clock);

    /// <summary>Vincula o repositório descoberto "stub:{externalId}" a um app, como a triagem faria.</summary>
    private async Task LinkToAppAsync(string externalId, string appId, string appName)
    {
        _apps.Add(new CheckApps.Domain.App { Id = appId, SquadId = "sq-1", Name = appName });
        await _apps.AddRepositoryAsync(new CheckApps.Domain.Repository
        {
            Id = Guid.NewGuid().ToString("n"), AppId = appId, Name = "vinculado", Provider = "stub", ExternalId = externalId
        });
    }

    private static DiscoveredRepositoryInfo Info(string id, string name, string? siglaApp = null, string? warning = null) =>
        new(id, name, $"https://example.invalid/{name}", Archived: false, PushedAt: T0, siglaApp, warning);

    [Fact]
    public async Task ScanAsync_StoresOnlyReposWithTheExactPrefix_NeverCreatesApps()
    {
        _source.Add("ab1", Info("1", "lab-ab1-app-pagamentos", "AB1-0001"));
        _source.Add("ab1", Info("2", "playground"));                 // fora da convenção
        _source.Add("ab1", Info("3", "lab-ab12-app-vizinho"));       // sigla maior, casa por trecho
        _source.Add("ab1", Info("4", "sandbox-lab-ab1-ruido"));      // contém, mas não começa

        var report = await Sut().ScanAsync(["ab1"], dryRun: false);

        var item = Assert.Single(report.Items);
        Assert.Equal("lab-ab1-app-pagamentos", item.Repository.Name);
        Assert.Equal("stub:1", item.Repository.Id);
        Assert.Equal("ab1", item.Repository.Sigla);
        Assert.Equal(RepositorySources.GithubScan, item.Repository.Source);
        Assert.Equal(T0, item.Repository.FirstSeenAt);
        Assert.Equal(1, _discovered.Count);
        Assert.Equal(UpsertOutcome.New, item.Outcome);
        Assert.Equal(RepositoryRoles.Application, item.Suggestion.Role);
    }

    [Fact]
    public async Task ScanAsync_Twice_IsNewThenUnchanged_AndKeepsTheTriageDecision()
    {
        _source.Add("ab1", Info("1", "lab-ab1-app-pagamentos", "AB1-0001"));
        var sut = Sut();

        var first = await sut.ScanAsync(["ab1"], dryRun: false);
        await _discovered.SetStatusAsync("stub:1", TriageStatus.Ignored);
        _clock.Advance(TimeSpan.FromDays(1));
        var second = await sut.ScanAsync(["ab1"], dryRun: false);

        Assert.Equal(1, first.Count(UpsertOutcome.New));
        Assert.Equal(1, second.Count(UpsertOutcome.Unchanged));
        var stored = await _discovered.GetAsync("stub:1");
        Assert.Equal(TriageStatus.Ignored, stored!.Status);
        Assert.Equal(T0.AddDays(1), stored.LastSeenAt);
        Assert.Equal(T0, stored.FirstSeenAt);
    }

    [Fact]
    public async Task ScanAsync_DryRun_ReportsWhatWouldHappen_ButWritesNothing()
    {
        _source.Add("ab1", Info("1", "lab-ab1-app-pagamentos", "AB1-0001"));
        _source.Add("ab1", Info("2", "lab-ab1-infra-fila"));
        await Sut().ScanAsync(["ab1"], dryRun: false);
        _source.Add("ab1", Info("3", "lab-ab1-app-novo", "AB1-0003"));

        var report = await Sut().ScanAsync(["ab1"], dryRun: true);

        Assert.True(report.DryRun);
        Assert.Equal(1, report.Count(UpsertOutcome.New));
        Assert.Equal(2, report.Count(UpsertOutcome.Unchanged));
        Assert.Equal(2, _discovered.Count);   // o terceiro NÃO foi gravado
    }

    [Fact]
    public async Task ScanAsync_DryRun_DetectsUpdatesToWhatWasObserved()
    {
        _source.Add("ab1", Info("1", "lab-ab1-app-pagamentos", "AB1-0001"));
        await Sut().ScanAsync(["ab1"], dryRun: false);

        var renamedSource = new StubDiscoverySource();
        renamedSource.Add("ab1", Info("1", "lab-ab1-app-pagamentos-v2", "AB1-0001"));
        var report = await new ScanService(renamedSource, _discovered, _options, _clock).ScanAsync(["ab1"], dryRun: true);

        Assert.Equal(1, report.Count(UpsertOutcome.Updated));
    }

    [Theory]
    [InlineData("")]
    [InlineData("lab-")]     // um trecho de nome, não uma sigla
    [InlineData("abcd")]
    public async Task ScanAsync_RejectsInvalidScope_BeforeQueryingTheSource(string sigla)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().ScanAsync([sigla], dryRun: false));

        Assert.Empty(_source.Calls);
    }

    [Fact]
    public async Task ScanAsync_RejectsNoSiglas_BeforeQueryingTheSource()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().ScanAsync(null, dryRun: false));
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().ScanAsync([], dryRun: false));

        Assert.Empty(_source.Calls);
    }

    [Fact]
    public async Task ScanAsync_WithoutNameTemplate_Throws_BeforeQueryingTheSource()
    {
        _options.NameTemplate = null;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Sut().ScanAsync(["ab1"], dryRun: false));

        Assert.Contains("NameTemplate", error.Message);
        Assert.Empty(_source.Calls);
    }

    [Fact]
    public async Task ScanAsync_MarksItemsThatAreAlreadyInTheCatalog_WithTheirApp_AndKeepsTheOutcome()
    {
        _source.Add("ab1", Info("1", "lab-ab1-app-pagamentos", "AB1-0001"));
        _source.Add("ab1", Info("2", "lab-ab1-infra-fila"));
        await Sut().ScanAsync(["ab1"], dryRun: false);          // as duas entram na caixa
        await LinkToAppAsync("1", "pagamentos", "Pagamentos");   // a pessoa cria o app a partir da primeira

        var report = await Sut().ScanAsync(["ab1"], dryRun: false);

        var inCatalog = report.Items.Single(i => i.Repository.ExternalId == "1");
        Assert.Equal("pagamentos", inCatalog.CatalogApp!.AppId);
        Assert.Equal("Pagamentos", inCatalog.CatalogApp.AppName);
        Assert.Equal(UpsertOutcome.Unchanged, inCatalog.Outcome);   // o resultado descreve os dados observados
        Assert.Null(report.Items.Single(i => i.Repository.ExternalId == "2").CatalogApp);
        Assert.Equal(1, report.InCatalogCount);
        Assert.Equal(2, report.Count(UpsertOutcome.Unchanged));     // as contagens se sobrepõem, não se somam
    }

    [Fact]
    public async Task ScanAsync_DryRun_AlsoMarksItemsAlreadyInTheCatalog()
    {
        _source.Add("ab1", Info("1", "lab-ab1-app-pagamentos", "AB1-0001"));
        await Sut().ScanAsync(["ab1"], dryRun: false);
        await LinkToAppAsync("1", "pagamentos", "Pagamentos");

        var preview = await Sut().ScanAsync(["ab1"], dryRun: true);

        Assert.Equal("Pagamentos", Assert.Single(preview.Items).CatalogApp!.AppName);
        Assert.Equal(1, preview.InCatalogCount);
    }

    [Fact]
    public async Task ScanAsync_ARepositoryNeverSeenButAlreadyLinked_IsNewAndInTheCatalog()
    {
        // vínculo manual feito antes de qualquer scan: o descoberto é novo, mas o repositório já é de um app
        await LinkToAppAsync("9", "legado", "Legado");
        _source.Add("ab1", Info("9", "lab-ab1-app-legado"));

        var item = Assert.Single((await Sut().ScanAsync(["ab1"], dryRun: true)).Items);

        Assert.Equal(UpsertOutcome.New, item.Outcome);
        Assert.Equal("legado", item.CatalogApp!.AppId);
    }

    [Fact]
    public async Task ScanAsync_WithNothingInTheCatalog_MarksNothing()
    {
        _source.Add("ab1", Info("1", "lab-ab1-app-pagamentos"));

        var report = await Sut().ScanAsync(["ab1"], dryRun: false);

        Assert.All(report.Items, i => Assert.Null(i.CatalogApp));
        Assert.Equal(0, report.InCatalogCount);
    }

    [Fact]
    public void ConfigurationProblem_IsNull_WhenTheTemplateAndTheSourceAreReady()
    {
        Assert.Null(Sut().ConfigurationProblem);
    }

    [Fact]
    public void ConfigurationProblem_ReportsAMissingTemplate_FirstAndWithoutValues()
    {
        _options.NameTemplate = null;
        _source.ConfigurationProblem = "A fonte também tem um problema.";

        var problem = Sut().ConfigurationProblem;

        Assert.Contains("NameTemplate", problem);
        Assert.DoesNotContain("A fonte também", problem);
    }

    [Fact]
    public async Task ScanAsync_WhenTheSourceReportsAProblem_ThrowsItsMessage_WithoutQueryingTheSource()
    {
        _source.ConfigurationProblem = "Configure GitHub:Token.";

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Sut().ScanAsync(["ab1"], dryRun: false));

        Assert.Equal("Configure GitHub:Token.", error.Message);
        Assert.Empty(_source.Calls);
    }

    [Fact]
    public async Task ScanAsync_InvalidScope_IsReportedBeforeAConfigurationProblem()
    {
        _source.ConfigurationProblem = "Configure GitHub:Token.";

        // a ordem importa para o CLI e para a tela: o escopo inválido é a primeira recusa
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().ScanAsync(["lab-"], dryRun: false));
    }

    [Fact]
    public async Task ScanAsync_WhenASearchExceedsTheLimit_WritesNothingEvenForTheOtherSiglas()
    {
        _source.Add("ab1", Info("1", "lab-ab1-app-pagamentos", "AB1-0001"));
        _source.FailOnSigla = "cd2";

        var error = await Assert.ThrowsAsync<ScanLimitExceededException>(() => Sut().ScanAsync(["ab1", "cd2"], dryRun: false));

        Assert.Equal("cd2", error.Sigla);
        Assert.Equal(0, _discovered.Count);      // a leitura inteira vem antes de qualquer gravação
    }

    [Fact]
    public async Task ScanAsync_WarnsOnSiglaAppDivergence_AndOnSourceWarnings_ButDoesNotFail()
    {
        _source.Add("ab1", Info("1", "lab-ab1-app-migrado", "CD2-0001"));
        _source.Add("ab1", Info("2", "lab-ab1-app-quebrado", warning: "o arquivo de configuração não é um YAML válido"));

        var report = await Sut().ScanAsync(["ab1"], dryRun: false);

        Assert.Equal(2, report.Items.Count);
        Assert.Contains(report.Warnings, w => w.Contains("CD2-0001") && w.Contains("'ab1'"));
        Assert.Contains(report.Warnings, w => w.Contains("lab-ab1-app-quebrado") && w.Contains("YAML"));
    }

    [Fact]
    public async Task ScanAsync_NormalizesSiglas_AndScansEach()
    {
        _source.Add("ab1", Info("1", "lab-ab1-app-a"));
        _source.Add("cd2", Info("2", "lab-cd2-app-b"));

        var report = await Sut().ScanAsync([" AB1", "cd2", "ab1"], dryRun: false);

        Assert.Equal(["ab1", "cd2"], _source.Calls);
        Assert.Equal(2, report.Items.Count);
    }
}
