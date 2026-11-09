using CheckApps.Application.Analysis;
using CheckApps.Application.Discovery;
using CheckApps.Domain;
using CheckApps.Domain.Analysis;
using CheckApps.Domain.Discovery;
using CheckApps.Infrastructure.Analysis;
using CheckApps.Infrastructure.Analysis.OpenApi;
using CheckApps.Infrastructure.Sqlite;
using Dapper;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.IntegrationTests.Sqlite;

/// <summary>
/// Os contratos (spec 021) de ponta a ponta sobre SQLite de verdade: a análise lê o contrato da fonte fake, grava as fotos, e o
/// serviço de leitura mostra os endpoints e o que mudou. Sem rede.
/// </summary>
public class ContractServiceTests(SqliteFixture fixture) : IClassFixture<SqliteFixture>
{
    private const string DemoRepository = "fake-ab1-contrato-api-pagamentos";

    private readonly SquadRepository _squads = new(fixture.ConnectionFactory);
    private readonly AppRepository _apps = new(fixture.ConnectionFactory);
    private readonly AnalysisRepository _analyses = new(fixture.ConnectionFactory, TestAnalysisWriters.All);

    private ContractService Service() => new(_apps, _analyses);

    private async Task<(string AppId, string RepoId, string Sigla)> SeedAsync(string role = RepositoryRoles.Contract)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var squadId = $"sq-{suffix}";
        var appId = $"app-{suffix}";
        var repoId = $"repo-{suffix}";
        var sigla = $"z{suffix[..2]}";
        await _squads.SaveAsync(new Squad { Id = squadId, Name = $"Squad {suffix}" });
        await _apps.SaveAsync(new App { Id = appId, SquadId = squadId, Name = $"app {suffix}" });
        await _apps.AddRepositoryAsync(new Repository
        {
            Id = repoId, AppId = appId, Name = DemoRepository, Provider = FakeRepositoryContentSource.ProviderName, ExternalId = repoId,
            Sigla = sigla, Role = role, Status = "active"
        });
        return (appId, repoId, sigla);
    }

    private Task<AnalysisReport> AnalyzeAsync(string sigla, int revision, bool force = false, bool dryRun = false, DateTimeOffset? at = null) =>
        new AnalysisService(
                new FakeRepositoryContentSource(revision), new ScannerRegistry([new OpenApiScanner()]),
                [new DependencyFactProcessor(), new IntegrationFactProcessor(new AnalysisOptions()), new ApiContractFactProcessor()],
                _apps, _analyses, new AnalysisOptions(), new ScanOptions { SiglaPattern = "^[a-z0-9]{3}$" },
                new FixedTime(at ?? DateTimeOffset.UtcNow))
            .AnalyzeAsync([sigla], dryRun, force);

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task Analyze_StoresEndpointsAndFields_AndReadsThemBack()
    {
        var (appId, _, sigla) = await SeedAsync();

        var report = await AnalyzeAsync(sigla, revision: 0);
        Assert.Contains(new AnalysisFactCount(ApiContractFactProcessor.FactLabel, 4), report.Items[0].FactCounts!);

        var contracts = await Service().GetForAppAsync(appId);
        var repository = Assert.Single(contracts.Repositories);
        Assert.Equal(ContractDeclaredIn.ContractRepository, repository.DeclaredIn);
        Assert.Equal(4, repository.Endpoints.Count);

        var simulation = Assert.Single(repository.Endpoints, e => e.Path == "/simulacoes");
        Assert.Equal("POST", simulation.Method);
        Assert.Equal("simular", simulation.DeclaredId);
        Assert.Equal(["simulacao"], simulation.Tags);
        Assert.Equal(["oauth (oauth2)"], simulation.Security);
        Assert.Equal("Pagamentos", simulation.ApiTitle);
        Assert.Equal("openapi.yaml", simulation.SourceFile);
        Assert.Equal(FactOrigins.Parser, simulation.Origin);
        Assert.Equal("openapi", simulation.ScannerId);

        var value = Assert.Single(simulation.Fields, f => f.Path == "valor");
        Assert.Equal(ApiFieldLocations.Request, value.Location);
        Assert.Equal("number", value.Type);
        Assert.True(value.Required);

        var list = Assert.Single(repository.Endpoints, e => e.Path == "/pagamentos");
        Assert.Equal(["aberto", "pago", "cancelado"], Assert.Single(list.Fields, f => f.Path == "situacao" && f.Location == ApiFieldLocations.Query).EnumValues);
    }

    [Fact]
    public async Task DeclaredIn_ComesFromTheRepositoryRole()
    {
        var (appId, _, sigla) = await SeedAsync(role: RepositoryRoles.Application);
        await AnalyzeAsync(sigla, revision: 0);

        Assert.Equal(ContractDeclaredIn.ApplicationRepository, (await Service().GetForAppAsync(appId)).Repositories[0].DeclaredIn);
    }

    [Fact]
    public async Task SameCommit_StoresNothingNew_AndDryRunNeverStores()
    {
        var (appId, repoId, sigla) = await SeedAsync();

        await AnalyzeAsync(sigla, revision: 0, dryRun: true);
        Assert.Null(await _analyses.GetCurrentAsync(repoId));

        await AnalyzeAsync(sigla, revision: 0);
        var again = await AnalyzeAsync(sigla, revision: 0);

        Assert.Equal(AnalysisOutcome.Unchanged, again.Items[0].Outcome);
        using var connection = fixture.ConnectionFactory.Create();
        Assert.Equal(1, await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM repository_analyses WHERE repository_id = @repoId", new { repoId }));
        Assert.Equal(4, (await Service().GetForAppAsync(appId)).Repositories[0].Endpoints.Count);
    }

    [Fact]
    public async Task FirstAnalysis_HasNothingToCompare_InsteadOfEverythingNew()
    {
        var (appId, _, sigla) = await SeedAsync();
        await AnalyzeAsync(sigla, revision: 0);

        var repository = Assert.Single((await Service().GetChangesForAppAsync(appId)).Repositories);

        Assert.Equal(ContractChangeStates.FirstAnalysis, repository.State);
        Assert.Empty(repository.Changes);
        Assert.Null(repository.Baseline);
    }

    [Fact]
    public async Task NeverAnalyzed_HasNoContract()
    {
        var (appId, _, _) = await SeedAsync();

        var repository = Assert.Single((await Service().GetChangesForAppAsync(appId)).Repositories);

        Assert.Equal(ContractChangeStates.NoContract, repository.State);
    }

    [Fact]
    public async Task NewCommit_ShowsWhatChanged_ClassifiedEndToEnd()
    {
        var (appId, _, sigla) = await SeedAsync();
        var t0 = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        await AnalyzeAsync(sigla, revision: 0, at: t0);
        await AnalyzeAsync(sigla, revision: 1, at: t0.AddDays(1));

        var repository = Assert.Single((await Service().GetChangesForAppAsync(appId)).Repositories);

        Assert.Equal(ContractChangeStates.Compared, repository.State);
        Assert.NotEqual(repository.Baseline!.CommitSha, repository.Analysis!.CommitSha);

        void AssertHas(string severity, string kind, string path, string? field = null) =>
            Assert.Contains(repository.Changes, c => c.Severity == severity && c.Kind == kind && c.Path == path && c.FieldPath == field);

        AssertHas(ContractChangeSeverities.Breaking, ContractChangeKinds.EndpointRemoved, "/relatorios");
        AssertHas(ContractChangeSeverities.Breaking, ContractChangeKinds.BecameRequired, "/simulacoes", "contaOrigem");
        AssertHas(ContractChangeSeverities.Breaking, ContractChangeKinds.TypeChanged, "/simulacoes", "valor");
        AssertHas(ContractChangeSeverities.Breaking, ContractChangeKinds.FieldRemoved, "/simulacoes", "valorImposto");
        AssertHas(ContractChangeSeverities.Breaking, ContractChangeKinds.EnumValueRemoved, "/pagamentos", "situacao");
        AssertHas(ContractChangeSeverities.Safe, ContractChangeKinds.FieldAdded, "/simulacoes", "perfil");
        AssertHas(ContractChangeSeverities.Safe, ContractChangeKinds.EndpointAdded, "/simulacoes/{id}");
        AssertHas(ContractChangeSeverities.Attention, ContractChangeKinds.EndpointDeprecated, "/pagamentos/{pagamentoId}");
        AssertHas(ContractChangeSeverities.Attention, ContractChangeKinds.EnumValueAdded, "/pagamentos", "[].situacao");

        // O parâmetro renomeado de {id} para {pagamentoId} mantém o endpoint e o campo: nada disso é uma mudança.
        Assert.DoesNotContain(repository.Changes, c => c.Kind == ContractChangeKinds.EndpointRemoved && c.Path.StartsWith("/pagamentos", StringComparison.Ordinal));
        Assert.DoesNotContain(repository.Changes, c => c.FieldPath is "id" or "pagamentoId");
    }

    [Fact]
    public async Task Force_OnTheSameCommit_KeepsComparingWithTheLastDifferentCommit()
    {
        var (appId, _, sigla) = await SeedAsync();
        var t0 = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        await AnalyzeAsync(sigla, revision: 0, at: t0);
        await AnalyzeAsync(sigla, revision: 1, at: t0.AddDays(1));
        var beforeForce = (await Service().GetChangesForAppAsync(appId)).Repositories[0];

        await AnalyzeAsync(sigla, revision: 1, force: true, at: t0.AddDays(2));
        var afterForce = (await Service().GetChangesForAppAsync(appId)).Repositories[0];

        Assert.Equal(beforeForce.Baseline!.CommitSha, afterForce.Baseline!.CommitSha);
        Assert.Equal(beforeForce.Changes.Count, afterForce.Changes.Count);
        Assert.NotEmpty(afterForce.Changes);
    }

    [Fact]
    public async Task AnalysisWithoutEndpoints_IsNotABaseline()
    {
        var (appId, repoId, sigla) = await SeedAsync();
        var t0 = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

        // Uma análise de antes desta spec: commit diferente, sem nenhum endpoint.
        await _analyses.SaveAsync(new RepositoryAnalysis
        {
            Id = Guid.NewGuid().ToString("n"), RepositoryId = repoId, AnalyzedAt = t0, CommitSha = "antigo", Status = AnalysisStatuses.Ok
        }, []);
        await AnalyzeAsync(sigla, revision: 0, at: t0.AddDays(1));

        Assert.Equal(ContractChangeStates.FirstAnalysis, (await Service().GetChangesForAppAsync(appId)).Repositories[0].State);
    }

    [Fact]
    public async Task PartialAnalysis_NeverReportsWhatDisappearedAsBreaking()
    {
        var (appId, repoId, sigla) = await SeedAsync();
        var t0 = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        await AnalyzeAsync(sigla, revision: 0, at: t0);

        // O arquivo não pôde ser lido nesta análise: nenhum endpoint, situação parcial.
        await _analyses.SaveAsync(new RepositoryAnalysis
        {
            Id = Guid.NewGuid().ToString("n"), RepositoryId = repoId, AnalyzedAt = t0.AddDays(1), CommitSha = "novo",
            Status = AnalysisStatuses.Partial, Warnings = ["openapi.yaml: maior que o limite de leitura; ignorado."]
        }, []);

        var repository = (await Service().GetChangesForAppAsync(appId)).Repositories[0];

        Assert.Equal(4, repository.Changes.Count);
        Assert.All(repository.Changes, c => Assert.Equal(ContractChangeSeverities.Attention, c.Severity));
    }

    private sealed class SingleFileSource(string path, string content) : IRepositoryContentSource
    {
        public string? ConfigurationProblem => null;

        public bool CanRead(Repository repository) => true;

        public Task<RepositoryTree> ListAsync(Repository repository, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RepositoryTree("sha-segredos", [new RepositoryFile(path, content.Length)]));

        public Task<string?> ReadFileAsync(Repository repository, string commitSha, string filePath, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(content);
    }

    [Fact]
    public async Task PlantedSecrets_NeverReachTheDatabaseOrTheWarnings()
    {
        const string contract =
            """
            openapi: 3.0.3
            info: { title: Com segredos, version: '1', description: SEGREDO-1 }
            servers: [ { url: 'https://usuario:SEGREDO-2@interno.exemplo.com' } ]
            x-amazon-apigateway-policy: SEGREDO-3
            paths:
              /pagamentos:
                post:
                  description: SEGREDO-4
                  x-amazon-apigateway-integration: { uri: 'https://SEGREDO-5.exemplo.com' }
                  requestBody:
                    content:
                      application/json:
                        example: { token: SEGREDO-6 }
                        schema:
                          type: object
                          properties:
                            token: { type: string, description: SEGREDO-7, default: SEGREDO-8, example: SEGREDO-9 }
                  responses:
                    '200': { description: SEGREDO-10 }
                    'SEGREDO-11': { description: invalido }
            """;

        var (_, repoId, sigla) = await SeedAsync();
        await new AnalysisService(
                new SingleFileSource("openapi.yaml", contract), new ScannerRegistry([new OpenApiScanner()]), [new ApiContractFactProcessor()],
                _apps, _analyses, new AnalysisOptions(), new ScanOptions { SiglaPattern = "^[a-z0-9]{3}$" }, TimeProvider.System)
            .AnalyzeAsync([sigla], dryRun: false);

        using var connection = fixture.ConnectionFactory.Create();
        var analysisId = await connection.ExecuteScalarAsync<string>("SELECT id FROM repository_analyses WHERE repository_id = @repoId", new { repoId });
        var rows = new List<object?>();
        rows.AddRange(await connection.QueryAsync("SELECT * FROM repository_analyses WHERE id = @analysisId", new { analysisId }));
        rows.AddRange(await connection.QueryAsync("SELECT * FROM repository_api_endpoints WHERE analysis_id = @analysisId", new { analysisId }));
        rows.AddRange(await connection.QueryAsync(
            "SELECT f.* FROM repository_api_fields f JOIN repository_api_endpoints e ON e.id = f.endpoint_id WHERE e.analysis_id = @analysisId", new { analysisId }));

        var everything = System.Text.Json.JsonSerializer.Serialize(rows);
        Assert.Contains("token", everything, StringComparison.Ordinal);
        Assert.DoesNotContain("SEGREDO", everything, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownApp_Throws()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service().GetForAppAsync("nao-existe"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service().GetChangesForAppAsync("nao-existe"));
    }

    [Fact]
    public async Task RemovingTheAnalysis_CascadesToEndpointsAndFields()
    {
        var (_, repoId, sigla) = await SeedAsync();
        await AnalyzeAsync(sigla, revision: 0);

        using var connection = fixture.ConnectionFactory.Create();
        Assert.True(await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM repository_api_fields f JOIN repository_api_endpoints e ON e.id = f.endpoint_id JOIN repository_analyses a ON a.id = e.analysis_id WHERE a.repository_id = @repoId",
            new { repoId }) > 0);

        await connection.ExecuteAsync("DELETE FROM repository_analyses WHERE repository_id = @repoId", new { repoId });

        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM repository_api_endpoints WHERE analysis_id NOT IN (SELECT id FROM repository_analyses)"));
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM repository_api_fields WHERE endpoint_id NOT IN (SELECT id FROM repository_api_endpoints)"));
    }
}
