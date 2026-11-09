using System.Text.Json;
using CheckApps.Application.Analysis;
using CheckApps.Domain.Analysis;
using CheckApps.Infrastructure.Analysis.OpenApi;

namespace CheckApps.UnitTests.Analysis;

/// <summary>O scanner de contratos (spec 021) sobre os repositórios fictícios de <c>Fixtures/OpenApi/</c>, sem rede.</summary>
public class OpenApiScannerTests
{
    private static string Root(string scenario) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "OpenApi", scenario);

    /// <summary>O mesmo caminho do serviço: lista, o scanner escolhe, lê-se só o escolhido, o scanner analisa.</summary>
    private static ScannerResult Scan(string scenario)
    {
        var root = Root(scenario);
        Assert.True(Directory.Exists(root), $"Fixture não encontrada na saída do build: {root}");

        var paths = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Where(p => p != "EXPECTED.md")
            .ToList();

        var scanner = new OpenApiScanner();
        Assert.True(scanner.AppliesTo(paths));
        return scanner.Analyze(scanner.SelectFiles(paths).ToDictionary(p => p, p => File.ReadAllText(Path.Combine(root, p))));
    }

    private static List<ScannerApiEndpoint> Endpoints(ScannerResult result) =>
        result.Outputs.OfType<ScannerApiEndpoint>().ToList();

    private static ScannerApiEndpoint Endpoint(ScannerResult result, string method, string path) =>
        Assert.Single(Endpoints(result), e => e.Method == method && e.Path == path);

    private static ScannerApiField Field(ScannerApiEndpoint endpoint, string location, string path) =>
        Assert.Single(endpoint.Fields, f => f.Location == location && f.Path == path);

    // ---- Escolha dos arquivos ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData("openapi.yaml", true)]
    [InlineData("docs/Swagger.JSON", true)]
    [InlineData("api.yml", true)]
    [InlineData("api-gateway.yaml", true)]
    [InlineData("contracts/simulacoes.yaml", true)]
    [InlineData("contrato/qualquer.json", true)]
    [InlineData("src/Foo.Api/foo.json", false)]
    [InlineData("config.yaml", false)]
    [InlineData("openapi.txt", false)]
    [InlineData("node_modules/x/openapi.json", false)]
    [InlineData("bin/Debug/swagger.json", false)]
    [InlineData("src/App/appsettings.contract.json", false)]
    [InlineData("package.json", false)]
    [InlineData("contracts/foo.schema.json", false)]
    public void AppliesTo_UsesTheFileNameAndIgnoresOrdinaryApiProjects(string path, bool expected) =>
        Assert.Equal(expected, new OpenApiScanner().AppliesTo([path]));

    [Fact]
    public void Patterns_ComeFromConfiguration_AndReplaceTheDefaults()
    {
        var scanner = new OpenApiScanner(new AnalysisOptions { OpenApiFilePatterns = ["minhaapi*", "especificacoes/"] });

        Assert.True(scanner.AppliesTo(["MinhaApi.yaml"]));
        Assert.True(scanner.AppliesTo(["especificacoes/a.json"]));
        Assert.False(scanner.AppliesTo(["openapi.yaml"]));
    }

    // ---- OpenAPI 3 e Swagger 2 ---------------------------------------------------------------------------------------

    [Fact]
    public void Yaml_StoresEndpointsWithProvenanceAndFlattenedFields()
    {
        var result = Scan("01-oas3-yaml");

        Assert.Empty(result.Warnings);
        Assert.Equal(3, Endpoints(result).Count);

        var post = Endpoint(result, "POST", "/simulacoes");
        Assert.Equal(ApiMechanisms.OpenApi, post.Mechanism);
        Assert.Equal("criarSimulacao", post.DeclaredId);
        Assert.Equal(["simulacao"], post.Tags);
        Assert.Equal("Simulacoes", post.ApiTitle);
        Assert.Equal("1.10", post.ApiVersion);
        Assert.Equal("contracts/simulacoes.yaml", post.SourceFile);
        Assert.Equal(Confidences.High, post.Confidence);
        Assert.Null(post.ConfidenceNote);

        var canal = Field(post, ApiFieldLocations.Header, "canal");
        Assert.True(canal.Required);
        Assert.Equal(["app", "web"], canal.EnumValues);

        var pagina = Field(post, ApiFieldLocations.Query, "pagina");
        Assert.False(pagina.Required);
        Assert.Equal("integer", pagina.Type);
        Assert.Equal("int32", pagina.Format);

        var valor = Field(post, ApiFieldLocations.Request, "valor");
        Assert.True(valor.Required);
        Assert.Equal("number", valor.Type);
        Assert.Equal("application/json", valor.MediaType);
        Assert.Null(valor.StatusCode);

        Assert.False(Field(post, ApiFieldLocations.Request, "contaOrigem").Required);
        Assert.Equal("array<object>", Field(post, ApiFieldLocations.Request, "cliente.contas").Type);
        Assert.True(Field(post, ApiFieldLocations.Request, "cliente.contas[].numero").Required);
        Assert.Equal("array<string>", Field(post, ApiFieldLocations.Request, "cliente.apelidos").Type);

        var situacao = Field(post, ApiFieldLocations.Response, "situacao");
        Assert.Equal("201", situacao.StatusCode);
        Assert.Equal(["aberta", "fechada"], situacao.EnumValues);
    }

    [Fact]
    public void Yaml_ReadsUnquotedStatusAndParametersDeclaredOnThePath()
    {
        var get = Endpoint(Scan("01-oas3-yaml"), "GET", "/simulacoes/{id}");

        Assert.True(Field(get, ApiFieldLocations.Path, "id").Required);
        Assert.Equal("200", Field(get, ApiFieldLocations.Response, "idSimulacao").StatusCode);
    }

    [Fact]
    public void Security_IsInheritedFromTheDocument_UnlessTheEndpointDeclaresItsOwn()
    {
        var result = Scan("01-oas3-yaml");

        Assert.Equal(["oauth (oauth2)"], Endpoint(result, "POST", "/simulacoes").Security);

        var delete = Endpoint(result, "DELETE", "/simulacoes/{id}");
        Assert.Empty(delete.Security);
        Assert.True(delete.Deprecated);
    }

    [Fact]
    public void Json_AcceptsCommentsAndTrailingCommas_AndABodyThatIsAList()
    {
        var result = Scan("02-oas3-json");

        Assert.Empty(result.Warnings);
        var get = Endpoint(result, "GET", "/itens");
        Assert.Equal("array<object>", Field(get, ApiFieldLocations.Response, OpenApiScanner.BodyPath).Type);
        Assert.Equal("string", Field(get, ApiFieldLocations.Response, "[].id").Type);
    }

    [Fact]
    public void Swagger2_FallsIntoTheSameModel_EvenWithAnUnquotedVersionAndNoMediaType()
    {
        var result = Scan("03-swagger2");

        Assert.Empty(result.Warnings);
        var post = Endpoint(result, "POST", "/simulacoes");
        Assert.Equal(["chave (apikey)"], post.Security);
        Assert.Equal("application/json", Field(post, ApiFieldLocations.Request, "valor").MediaType);
        Assert.True(Field(post, ApiFieldLocations.Request, "valor").Required);
        Assert.Equal("application/json", Field(post, ApiFieldLocations.Response, "valor").MediaType);
        Assert.Equal("integer", Field(post, ApiFieldLocations.Query, "pagina").Type);
    }

    // ---- Composição, ciclos e referências ----------------------------------------------------------------------------

    [Fact]
    public void AllOf_UnitesFieldsAndRequired_AndTheSameReferenceTwiceIsNotACycle()
    {
        var post = Endpoint(Scan("04-composicao"), "POST", "/composto");

        Assert.True(Field(post, ApiFieldLocations.Request, "codigo").Required);
        Assert.True(Field(post, ApiFieldLocations.Request, "valor").Required);
        Assert.Null(Field(post, ApiFieldLocations.Request, "codigo").Note);
    }

    [Fact]
    public void OneOf_ListsEveryVariant_NothingRequired_WithANoteAndMediumConfidence()
    {
        var post = Endpoint(Scan("04-composicao"), "POST", "/composto");

        var a = Field(post, ApiFieldLocations.Response, "a");
        Assert.False(a.Required);
        Assert.NotNull(a.Note);
        Assert.NotNull(Field(post, ApiFieldLocations.Response, "b").Note);
        Assert.Equal("string", Field(post, ApiFieldLocations.Response, "comum").Type);
        Assert.Equal(Confidences.Medium, post.Confidence);
        Assert.NotNull(post.ConfidenceNote);
    }

    [Fact]
    public void Cycle_Ends_RecordsTheFieldsUntilThere_AndIsANoteNotAWarning()
    {
        var result = Scan("04-composicao");
        var get = Endpoint(result, "GET", "/recursivo");

        Assert.Equal("string", Field(get, ApiFieldLocations.Response, "nome").Type);
        Assert.NotNull(Field(get, ApiFieldLocations.Response, "pai").Note);
        Assert.DoesNotContain(get.Fields, f => f.Path.StartsWith("pai.", StringComparison.Ordinal));
        Assert.Equal(Confidences.High, get.Confidence);
        Assert.Contains(result.Notes, n => n.Contains("recursivo", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Warnings, w => w.Contains("/recursivo", StringComparison.Ordinal));
    }

    [Fact]
    public void ExternalReference_IsNotFetched_AndIsAWarningWithMediumConfidence()
    {
        var result = Scan("04-composicao");
        var get = Endpoint(result, "GET", "/externo");

        Assert.Equal(Confidences.Medium, get.Confidence);
        Assert.NotNull(Field(get, ApiFieldLocations.Response, OpenApiScanner.BodyPath).Note);
        Assert.Contains(result.Warnings, w => w.Contains("/externo", StringComparison.Ordinal) && w.Contains("openapi.yaml", StringComparison.Ordinal));
    }

    [Fact]
    public void DanglingReference_IsAWarning_AndDoesNotBreakTheFile()
    {
        const string contract =
            """
            openapi: 3.0.3
            info: { title: t, version: '1' }
            paths:
              /a:
                get:
                  responses:
                    '200':
                      description: ok
                      content:
                        application/json:
                          schema: { $ref: '#/components/schemas/NaoExiste' }
              /b:
                get:
                  responses:
                    '200': { description: ok }
            """;

        var result = new OpenApiScanner().Analyze(new Dictionary<string, string> { ["openapi.yaml"] = contract });

        Assert.Equal(2, Endpoints(result).Count);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void DeepSchema_StopsAtTheLimit_WithAWarning()
    {
        var schema = "{ type: string }";
        for (var i = 0; i < 20; i++)
        {
            schema = $"{{ type: object, properties: {{ n: {schema} }} }}";
        }

        var contract =
            $$"""
            openapi: 3.0.3
            info: { title: t, version: '1' }
            paths:
              /fundo:
                post:
                  requestBody:
                    content:
                      application/json:
                        schema: {{schema}}
                  responses:
                    '200': { description: ok }
            """;

        var result = new OpenApiScanner().Analyze(new Dictionary<string, string> { ["openapi.yaml"] = contract });

        Assert.Contains(result.Warnings, w => w.Contains("limite", StringComparison.Ordinal));
        Assert.True(Endpoints(result)[0].Fields.Count <= 13);
    }

    [Fact]
    public void YamlAliasBomb_IsAWarning_NotAHang()
    {
        var lines = new List<string> { "openapi: 3.0.0", "a0: &a0 [x, x, x, x, x, x, x, x, x, x]" };
        for (var i = 1; i < 12; i++)
        {
            var refs = string.Join(", ", Enumerable.Repeat($"*a{i - 1}", 10));
            lines.Add($"a{i}: &a{i} [{refs}]");
        }

        var result = new OpenApiScanner().Analyze(new Dictionary<string, string> { ["openapi.yaml"] = string.Join('\n', lines) });

        Assert.Empty(Endpoints(result));
        Assert.Contains(result.Warnings, w => w.Contains("openapi.yaml", StringComparison.Ordinal));
    }

    // ---- Formato exportado do AWS API Gateway ------------------------------------------------------------------------

    [Fact]
    public void AwsApiGateway_ReadsEveryEndpoint_AndIgnoresTheXAmazonExtensions()
    {
        var result = Scan("07-aws-api-gateway");

        Assert.Empty(result.Warnings);
        Assert.Empty(result.Notes);
        Assert.Equal(5, Endpoints(result).Count);

        var simulation = Endpoint(result, "POST", "/fundos/simulacoes");
        Assert.Equal("simularAplicacao", simulation.DeclaredId);
        Assert.Equal(["autorizador-token (apikey)"], simulation.Security);
        Assert.Equal(Confidences.High, simulation.Confidence);
        Assert.True(Field(simulation, ApiFieldLocations.Request, "valor").Required);
        Assert.True(Field(simulation, ApiFieldLocations.Request, "cliente.contas[].numero").Required);
        Assert.False(Field(simulation, ApiFieldLocations.Request, "contaOrigem").Required);
        Assert.Equal("number", Field(simulation, ApiFieldLocations.Response, "valorImposto").Type);
        Assert.True(Field(simulation, ApiFieldLocations.Header, "x-correlation-id").Required);

        var list = Endpoint(result, "GET", "/fundos");
        Assert.Equal(["conservador", "moderado", "arrojado"], Field(list, ApiFieldLocations.Query, "perfilRisco").EnumValues);
        Assert.Equal("array<object>", Field(list, ApiFieldLocations.Response, "itens").Type);

        // O CORS (OPTIONS com integração mock) não tem corpo nem segurança: é um endpoint sem campos, não um erro.
        var cors = Endpoint(result, "OPTIONS", "/fundos");
        Assert.Empty(cors.Security);
        Assert.Empty(cors.Fields);

        Assert.Equal("simulacao", Endpoint(result, "POST", "/fundos/simulacoes").Tags[0]);
        Assert.Equal("/fundos/{codigoFundo}", Endpoint(result, "GET", "/fundos/{codigoFundo}").Path);
    }

    [Fact]
    public void AwsApiGateway_NothingFromTheXExtensionsLeaksOut()
    {
        var everything = JsonSerializer.Serialize(Endpoints(Scan("07-aws-api-gateway")));

        foreach (var planted in new[] { "SEGREDO", "123456789012", "amazonaws", "arn:aws", "stageVariables", "vpcLinkId", "exemplo.com" })
        {
            Assert.DoesNotContain(planted, everything, StringComparison.Ordinal);
        }
    }

    // ---- O que não é contrato ----------------------------------------------------------------------------------------

    [Fact]
    public void NonContracts_AreANote_ButBrokenContractsAreAWarningWithoutContent()
    {
        var result = Scan("05-nao-contrato");

        Assert.Empty(Endpoints(result));
        Assert.Contains(result.Notes, n => n.StartsWith("2 arquivo(s)", StringComparison.Ordinal));
        Assert.Equal(2, result.Warnings.Count);
        Assert.Contains(result.Warnings, w => w.StartsWith("contrato-quebrado.yaml:", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, w => w.StartsWith("contrato-erros.json:", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Warnings, w => w.Contains("isto nao fecha", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Warnings, w => w.Contains("appsettings", StringComparison.Ordinal));
    }

    // ---- Segredos ----------------------------------------------------------------------------------------------------

    [Fact]
    public void PlantedSecrets_NeverLeaveTheScanner()
    {
        var result = Scan("06-segredos");

        var post = Endpoint(result, "POST", "/pagamentos");
        Assert.Equal("string", Field(post, ApiFieldLocations.Request, "token").Type);

        var everything = JsonSerializer.Serialize(new { Endpoints = Endpoints(result), result.Warnings, result.Notes });
        Assert.DoesNotContain("SEGREDO", everything, StringComparison.Ordinal);
    }

    [Fact]
    public void SameContent_SameResult()
    {
        var first = JsonSerializer.Serialize(Endpoints(Scan("01-oas3-yaml")));
        var second = JsonSerializer.Serialize(Endpoints(Scan("01-oas3-yaml")));

        Assert.Equal(first, second);
    }
}
