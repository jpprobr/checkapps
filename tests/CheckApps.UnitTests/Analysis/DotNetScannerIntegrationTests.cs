using System.Text.Json;
using CheckApps.Application.Analysis;
using CheckApps.Domain.Analysis;
using CheckApps.Infrastructure.Analysis.DotNet;
using static CheckApps.UnitTests.Analysis.FixtureFiles;

namespace CheckApps.UnitTests.Analysis;

/// <summary>
/// A leitura de <c>appsettings*.json</c> (spec 014) contra <c>Fixtures/Dotnet/07-integracoes-appsettings</c>, conferida
/// com o <c>EXPECTED.md</c> do cenário: primeiro o que o scanner vê (candidatos brutos), depois o que o núcleo grava.
/// </summary>
public class DotNetScannerIntegrationTests
{
    private const string Scenario = "07-integracoes-appsettings";
    private const string Project = "src/Empresa.Checkout";

    private readonly DotNetScanner _scanner = new();

    private static NormalizedIntegrations Normalize(ScannerResult result) =>
        IntegrationNormalizer.Normalize(
            result.Integrations, "an-1", "dotnet", "1.1.0", EnvironmentNames.DefaultAliases, () => Guid.NewGuid().ToString("n"));

    private static string Row(RepositoryIntegration i) =>
        $"{i.SourceFile[(i.SourceFile.LastIndexOf('/') + 1)..]}|{i.Environment}|{i.EnvironmentKind}|{i.Reference}|{i.State}|{i.Host}|{i.Port}|{i.BasePath}|{i.Confidence}";

    [Fact]
    public void SelectsEverySettingsFile_AndNoSourceCode()
    {
        var selected = _scanner.SelectFiles(Paths(Scenario));

        Assert.Contains($"{Project}/appsettings.json", selected);
        Assert.Contains($"{Project}/appsettings.Development.json", selected);
        Assert.Contains($"{Project}/appsettings.Staging.json", selected);
        Assert.Contains($"{Project}/appsettings.Uat.json", selected);
        Assert.Contains($"{Project}/appsettings.Production.json", selected);
        Assert.DoesNotContain(selected, p => p.EndsWith(".cs"));   // a interface Refit é da spec 015
    }

    [Fact]
    public void TheScanner_SeesOnlyEndpointKeys_AndSkipsConnectionStringsAndSecrets()
    {
        var result = Scan(_scanner, Scenario);

        // 7 no base, 2 em Development, 1 em Staging, 1 em Uat, 2 em Production.
        Assert.Equal(13, result.Integrations.Count);
        Assert.DoesNotContain(result.Integrations, i => i.ConfigKey.StartsWith("ConnectionStrings", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Integrations, i => i.ConfigKey.EndsWith("ApiKey", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Integrations, i => i.ConfigKey.EndsWith("LinkAjuda", StringComparison.Ordinal));   // não é chave de endereço
        Assert.All(result.Integrations, i => Assert.Equal(Project, i.ProjectPath));
    }

    [Fact]
    public void TheCore_RecordsTheTenExpectedIntegrations()
    {
        var normalized = Normalize(Scan(_scanner, Scenario));

        Assert.Equal(
            Sorted(
                "appsettings.json|||IPagamentosServiceCliente|parametrizada|empresa.{env}.exemplo.com||/pagamentos|media",
                "appsettings.json|||IClientesServiceCliente|parametrizada|empresa.{env}.exemplo.com||/clientes|media",
                "appsettings.json|||INotificacoesServiceCliente|resolvida|notificacoes.exemplo.com||/v1|alta",
                "appsettings.json|||IFaturasServiceCliente|nao-resolvida||||baixa",
                "appsettings.json|||IEstoqueServiceCliente|nao-resolvida||||baixa",
                "appsettings.Development.json|Development|dev|IPagamentosServiceCliente|resolvida|empresa.dev.exemplo.com||/pagamentos|alta",
                "appsettings.Staging.json|Staging|hom|IPagamentosServiceCliente|resolvida|empresa.hom.exemplo.com||/pagamentos|alta",
                "appsettings.Uat.json|Uat||IPagamentosServiceCliente|resolvida|empresa.uat.exemplo.com||/pagamentos|alta",
                "appsettings.Production.json|Production|prod|IPagamentosServiceCliente|resolvida|empresa.exemplo.com||/pagamentos|alta",
                "appsettings.Production.json|Production|prod|IFaturasServiceCliente|resolvida|faturas.exemplo.com|8443|/api|alta"),
            normalized.Items.Select(Row).Order(StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void UnresolvedValues_AreWarnings_AndIgnoredOnesAreANote()
    {
        var normalized = Normalize(Scan(_scanner, Scenario));

        Assert.Equal(2, normalized.Warnings.Count);   // valor vazio e ${FATURAS_URL}
        Assert.All(normalized.Warnings, w => Assert.Contains("integração não resolvida", w));

        var note = Assert.Single(normalized.Notes);   // 2 localhost + 1 amqp://
        Assert.Contains("3 valores ignorados", note);
        Assert.Contains("2 locais", note);
        Assert.Contains("1 sem URL http(s)", note);
    }

    [Fact]
    public void NoPlantedSecret_AppearsInAnythingTheCoreProduces()
    {
        var result = Scan(_scanner, Scenario);
        var normalized = Normalize(result);

        var everything = JsonSerializer.Serialize(normalized) + string.Join('\n', result.Warnings);

        Assert.DoesNotContain("SEGREDO-PLANTADO", everything);
        Assert.DoesNotContain("usuario", everything);
        Assert.DoesNotContain("Password", everything);
    }

    [Fact]
    public void TheDotNetFactsAreStillExtracted_AlongsideTheIntegrations()
    {
        var result = Scan(_scanner, Scenario);

        Assert.Equal(
            Sorted(
                $"{Project}/Empresa.Checkout.csproj|framework|net8.0||alta",
                $"{Project}/Empresa.Checkout.csproj|package|Refit.HttpClientFactory|7.2.1|alta"),
            Rows(result));
    }

    [Fact]
    public void ScenariosWithoutSettingsFiles_ProduceNoIntegration()
    {
        Assert.Empty(Scan(_scanner, "01-simples").Integrations);
        Assert.Empty(Scan(_scanner, "02-gerenciamento-central").Integrations);
    }

    [Fact]
    public void InvalidJson_IsAWarningWithThePath_NeverTheContent()
    {
        var files = new Dictionary<string, string>
        {
            ["App/App.csproj"] = "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>",
            ["App/appsettings.json"] = "{ \"A\": { \"BaseUrl\": \"https://a.exemplo.com\" ",   // truncado
            ["App/appsettings.Production.json"] = """{ "B": { "Url": "https://b.exemplo.com" } }"""
        };

        var result = _scanner.Analyze(files);

        var warning = Assert.Single(result.Warnings);
        Assert.Contains("App/appsettings.json", warning);
        Assert.DoesNotContain("a.exemplo.com", warning);
        Assert.Single(result.Integrations);                       // o outro arquivo segue
        Assert.Equal("Production", result.Integrations[0].EnvironmentName);
    }

    [Fact]
    public void TheJsonIsReadLeniently_LikeTheDotNetConfigurationDoes()
    {
        var files = new Dictionary<string, string>
        {
            ["App/App.csproj"] = "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>",
            ["App/appsettings.json"] = "﻿{ // comentário\n \"S\": { \"Url\": \"https://s.exemplo.com\", }, /* bloco */ }"
        };

        var integration = Assert.Single(_scanner.Analyze(files).Integrations);

        Assert.Equal("S", integration.Reference);
        Assert.Equal("S:Url", integration.ConfigKey);
    }

    [Fact]
    public void NestedSections_AndArrays_UseTheConfigurationKeyAndTheNearestSection()
    {
        var files = new Dictionary<string, string>
        {
            ["App/App.csproj"] = "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>",
            ["App/appsettings.json"] = """
                { "Integracoes": { "IPagamentosServiceCliente": { "BaseUrl": "https://p.exemplo.com" } },
                  "Servicos": [ { "Url": "https://s0.exemplo.com" }, { "Url": "https://s1.exemplo.com" } ],
                  "Host": "https://raiz.exemplo.com" }
                """
        };

        var found = _scanner.Analyze(files).Integrations.ToDictionary(i => i.ConfigKey, i => i.Reference);

        Assert.Equal("IPagamentosServiceCliente", found["Integracoes:IPagamentosServiceCliente:BaseUrl"]);
        Assert.Equal("0", found["Servicos:0:Url"]);
        Assert.Equal("1", found["Servicos:1:Url"]);
        Assert.Equal("(raiz)", found["Host"]);
    }

    [Fact]
    public void TheKeyHints_AreConfigurable_AndSecretNamesAreNeverEmitted()
    {
        var scanner = new DotNetScanner(new AnalysisOptions { IntegrationKeyHints = ["Address", "Token"] });
        var files = new Dictionary<string, string>
        {
            ["App/App.csproj"] = "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>",
            ["App/appsettings.json"] = """{ "S": { "Address": "https://s.exemplo.com", "Token": "https://t.exemplo.com", "BaseUrl": "https://b.exemplo.com" } }"""
        };

        var integration = Assert.Single(scanner.Analyze(files).Integrations);

        Assert.Equal("S:Address", integration.ConfigKey);   // BaseUrl saiu da lista; Token é nome de segredo
    }

    [Fact]
    public void SettingsInIgnoredFolders_AreNotRead()
    {
        var paths = new[] { "App/App.csproj", "App/appsettings.json", "App/bin/Debug/appsettings.json", "App/obj/appsettings.Production.json" };

        var selected = _scanner.SelectFiles(paths);

        Assert.Equal(["App/App.csproj", "App/appsettings.json"], selected);
    }

    [Fact]
    public void TheScannerVersion_ReflectsTheNewCapability() => Assert.Equal("1.1.0", _scanner.Version);
}
