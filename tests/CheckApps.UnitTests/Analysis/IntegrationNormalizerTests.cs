using System.Text.Json;
using CheckApps.Application.Analysis;
using CheckApps.Domain.Analysis;

namespace CheckApps.UnitTests.Analysis;

/// <summary>A redação do núcleo (spec 014, FR-004): só o endereço normalizado sai; segredo e valor bruto nunca.</summary>
public class IntegrationNormalizerTests
{
    private static NormalizedIntegrations Run(string raw, string? environmentName = null, string key = "ISvc:BaseUrl", string file = "src/App/appsettings.json") =>
        IntegrationNormalizer.Normalize(
            [new ScannerIntegration(IntegrationMechanisms.Config, "src/App", file, key.Split(':')[0], key, environmentName, raw)],
            "an-1", "dotnet", "1.1.0", EnvironmentNames.DefaultAliases, () => "id-1");

    private static RepositoryIntegration Single(string raw, string? environmentName = null) => Assert.Single(Run(raw, environmentName).Items);

    [Fact]
    public void ConcreteUrl_IsNormalized_AndResolved()
    {
        var integration = Single("https://Pagamentos.Exemplo.com/v1/");

        Assert.Equal(IntegrationStates.Resolved, integration.State);
        Assert.Equal(Confidences.High, integration.Confidence);
        Assert.Equal("https", integration.Scheme);
        Assert.Equal("pagamentos.exemplo.com", integration.Host);
        Assert.Null(integration.Port);
        Assert.Equal("/v1", integration.BasePath);
        Assert.Equal("ISvc", integration.Reference);
        Assert.Equal("ISvc:BaseUrl", integration.ConfigKey);
    }

    [Theory]
    [InlineData("https://a.exemplo.com:443/", null)]   // porta padrão do esquema
    [InlineData("http://a.exemplo.com:80/", null)]
    [InlineData("https://a.exemplo.com:8443/api/", 8443)]
    [InlineData("http://a.exemplo.com:8080", 8080)]
    public void ExplicitPort_IsKept_OnlyWhenItIsNotTheDefault(string raw, int? expectedPort) =>
        Assert.Equal(expectedPort, Single(raw).Port);

    [Fact]
    public void EnvPlaceholderInTheHost_IsATemplate_NotAGuess()
    {
        var integration = Single("http://empresa.{env}.exemplo.com/pagamentos");

        Assert.Equal(IntegrationStates.Parameterized, integration.State);
        Assert.Equal(Confidences.Medium, integration.Confidence);
        Assert.Equal("empresa.{env}.exemplo.com", integration.Host);
        Assert.Equal("/pagamentos", integration.BasePath);
        Assert.NotNull(integration.ConfidenceNote);
    }

    [Fact]
    public void UserInfoAndQuery_AreDiscarded_NothingSecretSurvives()
    {
        var result = Run("https://usuario:SEGREDO-PLANTADO-3@notificacoes.exemplo.com/v1/?key=SEGREDO-PLANTADO-4#frag");
        var integration = Assert.Single(result.Items);

        Assert.Equal("notificacoes.exemplo.com", integration.Host);
        Assert.Equal("/v1", integration.BasePath);

        var everything = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("SEGREDO-PLANTADO", everything);
        Assert.DoesNotContain("usuario", everything);
        Assert.DoesNotContain("frag", everything);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("${FATURAS_URL}")]
    [InlineData("#{Url}#")]
    [InlineData("%FATURAS_URL%")]
    [InlineData("https://${HOST}/api")]
    [InlineData("{{faturas}}")]
    public void EmptyOrSubstitutionValue_IsUnresolved_WithAWarning_AndNoHost(string raw)
    {
        var result = Run(raw);
        var integration = Assert.Single(result.Items);

        Assert.Equal(IntegrationStates.Unresolved, integration.State);
        Assert.Equal(Confidences.Low, integration.Confidence);
        Assert.Null(integration.Host);
        Assert.Single(result.Warnings);
        Assert.DoesNotContain("FATURAS", string.Concat(result.Warnings) + integration.ConfidenceNote);
        Assert.Contains("src/App/appsettings.json", result.Warnings[0]);
    }

    [Theory]
    [InlineData("http://localhost:5001")]
    [InlineData("https://LOCALHOST/api")]
    [InlineData("http://127.0.0.1:8080")]
    [InlineData("http://[::1]:9000/x")]
    public void LocalAddresses_AreIgnored_AndCountedInANote(string raw)
    {
        var result = Run(raw);

        Assert.Empty(result.Items);
        Assert.Empty(result.Warnings);
        Assert.Contains("1 valor ignorado", Assert.Single(result.Notes));
        Assert.Contains("local", result.Notes[0]);
    }

    [Theory]
    [InlineData("amqp://fila.exemplo.com:5672")]
    [InlineData("Server=db.exemplo.com;Password=SEGREDO-PLANTADO-1;")]
    [InlineData("pagamentos.exemplo.com")]
    [InlineData("ftp://arquivos.exemplo.com")]
    public void NonHttpValues_AreIgnored_AndNeverEchoed(string raw)
    {
        var result = Run(raw);

        Assert.Empty(result.Items);
        Assert.Empty(result.Warnings);
        var note = Assert.Single(result.Notes);
        Assert.Contains("sem URL http(s)", note);
        Assert.DoesNotContain("SEGREDO", note);
        Assert.DoesNotContain("exemplo.com", note);
    }

    [Fact]
    public void NotesAreAggregated_InASingleLine()
    {
        var candidates = new[] { "http://localhost:1", "http://localhost:2", "amqp://x" }
            .Select((raw, i) => new ScannerIntegration("config", "p", "a.json", "S", $"S{i}:Url", null, raw));

        var result = IntegrationNormalizer.Normalize(candidates, "an", "dotnet", "1", EnvironmentNames.DefaultAliases, () => "id");

        var note = Assert.Single(result.Notes);
        Assert.Contains("3 valores ignorados", note);
        Assert.Contains("2 locais", note);
        Assert.Contains("1 sem URL http(s)", note);
    }

    [Theory]
    [InlineData("Development", "dev")]
    [InlineData("Staging", "hom")]
    [InlineData("Production", "prod")]
    [InlineData("Uat", null)]
    public void EnvironmentName_IsKept_AndClassifiedOnlyWhenKnown(string name, string? kind)
    {
        var integration = Single("https://a.exemplo.com", name);

        Assert.Equal(name, integration.Environment);
        Assert.Equal(kind, integration.EnvironmentKind);
    }

    [Fact]
    public void BaseFile_HasNoEnvironment()
    {
        var integration = Single("https://a.exemplo.com");

        Assert.Null(integration.Environment);
        Assert.Null(integration.EnvironmentKind);
    }

    [Fact]
    public void SameFileAndKey_IsReportedOnce()
    {
        var candidate = new ScannerIntegration("config", "p", "a.json", "S", "S:Url", null, "https://a.exemplo.com");

        var result = IntegrationNormalizer.Normalize([candidate, candidate], "an", "dotnet", "1", EnvironmentNames.DefaultAliases, () => Guid.NewGuid().ToString("n"));

        Assert.Single(result.Items);
    }

    [Fact]
    public void ProvenanceIsFilled_FromTheScanner()
    {
        var integration = Single("https://a.exemplo.com");

        Assert.Equal(FactOrigins.Parser, integration.Origin);
        Assert.Equal("dotnet", integration.ScannerId);
        Assert.Equal("1.1.0", integration.ScannerVersion);
        Assert.Equal("an-1", integration.AnalysisId);
    }
}

public class EnvironmentNamesTests
{
    [Theory]
    [InlineData("Dev", "dev")]
    [InlineData("development", "dev")]
    [InlineData("Hom", "hom")]
    [InlineData("HOMOLOG", "hom")]
    [InlineData("Homologacao", "hom")]
    [InlineData("staging", "hom")]
    [InlineData("Prod", "prod")]
    [InlineData("Production", "prod")]
    [InlineData("Uat", null)]
    [InlineData("Local", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Classify_UsesTheAliasTable_IgnoringCase(string? name, string? expected) =>
        Assert.Equal(expected, EnvironmentNames.Classify(name, EnvironmentNames.DefaultAliases));

    [Fact]
    public void Classify_UsesTheConfiguredAliases()
    {
        var aliases = new Dictionary<string, string[]> { ["hom"] = ["Uat"] };

        Assert.Equal("hom", EnvironmentNames.Classify("Uat", aliases));
        Assert.Null(EnvironmentNames.Classify("Staging", aliases));
    }

    [Fact]
    public void Options_FallBackToTheDefaults_WhenNothingIsConfigured()
    {
        var options = new AnalysisOptions();

        Assert.Equal(["BaseUrl", "Url", "Endpoint", "Host"], options.EffectiveIntegrationKeyHints);
        Assert.Same(EnvironmentNames.DefaultAliases, options.EffectiveEnvironmentAliases);
    }

    [Fact]
    public void Options_ReplaceTheDefaults_WhenConfigured()
    {
        var options = new AnalysisOptions { IntegrationKeyHints = ["Address"], EnvironmentAliases = new() { ["dev"] = ["Local"] } };

        Assert.Equal(["Address"], options.EffectiveIntegrationKeyHints);
        Assert.Equal("dev", EnvironmentNames.Classify("Local", options.EffectiveEnvironmentAliases));
    }
}
