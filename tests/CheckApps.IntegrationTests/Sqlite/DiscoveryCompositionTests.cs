using CheckApps.Application.DependencyInjection;
using CheckApps.Application.Discovery;
using CheckApps.Application.Persistence;
using CheckApps.Infrastructure.DependencyInjection;
using CheckApps.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CheckApps.IntegrationTests.Sqlite;

/// <summary>
/// A composição do scan é a mesma para a API e para a ferramenta de linha de comando. Estes testes
/// montam um provedor real com a validação de DI do ambiente Development ligada — foi ela que já
/// quebrou a API uma vez — e **não usam rede**: a fonte GitHub só é exercitada sem credencial.
/// </summary>
public class DiscoveryCompositionTests : IDisposable
{
    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), "checkapps-tests", $"composition-{Guid.NewGuid():N}.db");

    private ServiceProvider Build(params (string Key, string Value)[] settings)
    {
        var values = new Dictionary<string, string?> { ["Sqlite:DatabasePath"] = _databasePath };
        foreach (var (key, value) in settings)
        {
            values[key] = value;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var services = new ServiceCollection();
        services.AddCheckAppsInfrastructure(configuration);
        services.AddCheckAppsApplication();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    [Fact]
    public async Task FakeSource_ComposesWithValidatedDI_ScansAndFeedsTheInbox_WithoutAnyConfiguration()
    {
        await using var provider = Build(("Scan:Source", "fake"));
        await provider.GetRequiredService<SqliteSchemaBootstrapper>().EnsureSchemaAsync();

        using var scope = provider.CreateScope();
        var scan = scope.ServiceProvider.GetRequiredService<ScanService>();
        var triage = scope.ServiceProvider.GetRequiredService<TriageService>();

        Assert.Null(scan.ConfigurationProblem);
        Assert.Equal("fake-{sigla}-", provider.GetRequiredService<ScanOptions>().NameTemplate);

        var first = await scan.ScanAsync(["ab1"], dryRun: false);
        var second = await scan.ScanAsync(["ab1"], dryRun: false);

        Assert.Equal(7, first.Count(UpsertOutcome.New));
        Assert.Equal(7, second.Count(UpsertOutcome.Unchanged));
        Assert.Equal(7, (await triage.ListAsync(Domain.Discovery.TriageStatus.New, "ab1")).Count);
    }

    [Fact]
    public async Task FakeSource_RespectsAConfiguredTemplate()
    {
        await using var provider = Build(("Scan:Source", "fake"), ("Scan:NameTemplate", "x-{sigla}-"));

        Assert.Equal("x-{sigla}-", provider.GetRequiredService<ScanOptions>().NameTemplate);
    }

    [Fact]
    public async Task GitHubSource_WithoutCredentials_ComposesFine_ExplainsWhatIsMissing_AndNeverGoesOnline()
    {
        // Sem token e sem organização: a API precisa subir mesmo assim, e a tela mostra o motivo.
        await using var provider = Build(("Scan:NameTemplate", "lab-{sigla}-"), ("GitHub:ApiBaseUrl", "http://127.0.0.1:1"));
        await provider.GetRequiredService<SqliteSchemaBootstrapper>().EnsureSchemaAsync();

        using var scope = provider.CreateScope();
        var scan = scope.ServiceProvider.GetRequiredService<ScanService>();

        Assert.Contains("GitHub:Token", scan.ConfigurationProblem);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => scan.ScanAsync(["ab1"], dryRun: false));
        Assert.Contains("GitHub:Token", error.Message);
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<TriageService>());
    }

    [Fact]
    public async Task GitHubSource_WithTokenButNoTemplate_ReportsTheTemplateFirst_WithoutLeakingTheToken()
    {
        const string token = "token-falso-SEGREDO-DE-TESTE-456";
        await using var provider = Build(("GitHub:Token", token), ("GitHub:Organization", "org-de-teste"));

        using var scope = provider.CreateScope();
        var problem = scope.ServiceProvider.GetRequiredService<ScanService>().ConfigurationProblem;

        Assert.Contains("NameTemplate", problem);
        Assert.DoesNotContain(token, problem);
    }

    [Fact]
    public async Task GitHubSource_FullyConfigured_ReportsReady()
    {
        await using var provider = Build(
            ("GitHub:Token", "token-falso"), ("GitHub:Organization", "org-de-teste"), ("Scan:NameTemplate", "lab-{sigla}-"));

        using var scope = provider.CreateScope();

        Assert.Null(scope.ServiceProvider.GetRequiredService<ScanService>().ConfigurationProblem);
    }

    [Fact]
    public void UnknownSource_IsRejectedAtComposition_WithAClearMessage()
    {
        var error = Assert.Throws<InvalidOperationException>(() => Build(("Scan:Source", "gitlab")));

        Assert.Contains("github ou fake", error.Message);
    }

    [Fact]
    public async Task EnabledIsOffByDefault_AndOnlyTurnsOnWhenExplicit()
    {
        await using var off = Build(("Scan:Source", "fake"));
        await using var on = Build(("Scan:Source", "fake"), ("Scan:Enabled", "true"));

        Assert.False(off.GetRequiredService<ScanOptions>().Enabled);
        Assert.True(on.GetRequiredService<ScanOptions>().Enabled);
    }

    // ---- Análise de repositórios (spec 013) -----------------------------------------------------------------------

    [Fact]
    public async Task Analysis_FakeSource_ComposesWithValidatedDI_AnalyzesACatalogedRepository_AndTheApiServiceReadsIt()
    {
        await using var provider = Build(("Scan:Source", "fake"), ("Analysis:InternalPackagePrefixes:0", "Empresa."));
        await provider.GetRequiredService<SqliteSchemaBootstrapper>().EnsureSchemaAsync();

        using var scope = provider.CreateScope();
        var squads = scope.ServiceProvider.GetRequiredService<Application.Persistence.ISquadRepository>();
        var apps = scope.ServiceProvider.GetRequiredService<Application.Persistence.IAppRepository>();
        await squads.SaveAsync(new Domain.Squad { Id = "sq-1", Name = "Squad" });
        await apps.SaveAsync(new Domain.App { Id = "app-1", SquadId = "sq-1", Name = "Pagamentos" });
        await apps.AddRepositoryAsync(new Domain.Repository
        {
            Id = "r1", AppId = "app-1", Name = "fake-ab1-app-pagamentos", Provider = "fake", ExternalId = "1",
            Sigla = "ab1", Status = "active"
        });

        var analysis = scope.ServiceProvider.GetRequiredService<Application.Analysis.AnalysisService>();
        Assert.Null(analysis.ConfigurationProblem);

        var first = await analysis.AnalyzeAsync(["ab1"], dryRun: false);
        var second = await analysis.AnalyzeAsync(["ab1"], dryRun: false);

        Assert.Equal(1, first.Count(Application.Analysis.AnalysisOutcome.Analyzed));
        Assert.Equal(1, second.Count(Application.Analysis.AnalysisOutcome.Unchanged));   // mesmo commit: nada novo

        var dependencies = await scope.ServiceProvider.GetRequiredService<Application.Analysis.DependencyService>().GetForAppAsync("app-1");
        var repository = Assert.Single(dependencies.Repositories);
        Assert.Contains(repository.Frameworks, f => f.Fact.Name == "net9.0");
        Assert.Contains(repository.Packages, p => p.Fact.Name == "Empresa.Contratos.Cliente" && p.IsInternal);
        Assert.Contains(repository.Packages, p => p.Fact.Name == "Serilog" && !p.IsInternal && p.Fact.Version == "3.1.1");
    }

    [Fact]
    public async Task Analysis_GitHubSource_WithoutAToken_ComposesFine_ExplainsWhatIsMissing_AndNeverGoesOnline()
    {
        await using var provider = Build(("GitHub:ApiBaseUrl", "http://127.0.0.1:1"));
        await provider.GetRequiredService<SqliteSchemaBootstrapper>().EnsureSchemaAsync();

        using var scope = provider.CreateScope();
        var analysis = scope.ServiceProvider.GetRequiredService<Application.Analysis.AnalysisService>();

        Assert.Contains("GitHub:Token", analysis.ConfigurationProblem);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => analysis.AnalyzeAsync(["ab1"], dryRun: false));
        Assert.Contains("GitHub:Token", error.Message);
    }

    [Fact]
    public async Task Analysis_Options_HaveSafeDefaults_AndTheDotNetScannerIsRegistered()
    {
        await using var provider = Build(("Scan:Source", "fake"));

        var options = provider.GetRequiredService<Application.Analysis.AnalysisOptions>();

        Assert.Empty(options.InternalPackagePrefixes);   // sem configuração, nada é interno
        Assert.True(options.MaxProjectFiles > 0 && options.MaxFileBytes > 0);
        Assert.Contains(provider.GetRequiredService<Application.Analysis.ScannerRegistry>().All, s => s.Id == "dotnet");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _databasePath, $"{_databasePath}-wal", $"{_databasePath}-shm" })
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // Arquivo temporário preso pelo SO não deve falhar a suíte.
            }
        }
    }
}
