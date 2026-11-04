using CheckApps.Application;
using CheckApps.Application.Analysis;
using CheckApps.Domain;
using CheckApps.Domain.Analysis;
using CheckApps.Infrastructure.Sqlite;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.IntegrationTests.Sqlite;

/// <summary>
/// Spec 017 com o SQLite de verdade: o repositório de vínculos, o serviço que os grava e as leituras da 014 enriquecidas
/// com o vínculo (hosts, detalhe, filtro e destino de cada integração de um app). Banco só desta classe.
/// </summary>
public class HostLinkTests(SqliteFixture fixture) : IClassFixture<SqliteFixture>
{
    private static readonly DateTimeOffset T1 = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private readonly SquadRepository _squads = new(fixture.ConnectionFactory);
    private readonly AppRepository _apps = new(fixture.ConnectionFactory);
    private readonly AnalysisRepository _analyses = new(fixture.ConnectionFactory);
    private readonly HostLinkRepository _links = new(fixture.ConnectionFactory);
    private readonly HostExternalRepository _externals = new(fixture.ConnectionFactory);
    private readonly AnalysisOptions _options = new();

    private HostLinkService Links() => new(_apps, _links, _externals, _analyses, _options, TimeProvider.System);

    private IntegrationService Integrations() => new(_apps, _analyses, _links, _externals, _options);

    private async Task<string> SeedSquadAsync()
    {
        var id = $"sq-{Guid.NewGuid():N}";
        await _squads.SaveAsync(new Squad { Id = id, Name = $"Squad {id[3..9]}" });
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

    private static RepositoryIntegration Integration(string analysisId, string? host, string reference, string? environment = null, string? kind = null) =>
        new()
        {
            Id = Guid.NewGuid().ToString("n"), AnalysisId = analysisId, Mechanism = IntegrationMechanisms.Config,
            Scheme = host is null ? null : "https", Host = host, Reference = reference, ConfigKey = $"{reference}:BaseUrl",
            Environment = environment, EnvironmentKind = kind,
            State = host is null ? IntegrationStates.Unresolved : host.Contains('{') ? IntegrationStates.Parameterized : IntegrationStates.Resolved,
            ProjectPath = "src/App", SourceFile = "src/App/appsettings.json", Confidence = Confidences.High,
            ScannerId = "dotnet", ScannerVersion = "1.1.0"
        };

    private async Task AnalyzeAsync(string repoId, params string?[] hosts)
    {
        var analysis = new RepositoryAnalysis
        {
            Id = Guid.NewGuid().ToString("n"), RepositoryId = repoId, AnalyzedAt = T1, CommitSha = $"sha-{Guid.NewGuid():N}", Status = AnalysisStatuses.Ok
        };
        await _analyses.AddAsync(analysis, [], hosts.Select(h => Integration(analysis.Id, h, "ISvc")).ToList());
    }

    private static string Marker() => Guid.NewGuid().ToString("n")[..8];

    // ---- Repositório -----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Repository_Upsert_CreatesThenChangesTheSamePair_KeepingOneRow_AndComparesKeysInLowerCase()
    {
        var (appId, _) = await SeedAppAsync(await SeedSquadAsync(), "App");
        var host = $"a-{Marker()}.exemplo.com";

        await _links.UpsertAsync(new HostLink { Id = "id-1", HostKey = host.ToUpperInvariant(), AppId = appId, State = HostLinkStates.Rejected, DecidedAt = T1, Note = "antes" });
        await _links.UpsertAsync(new HostLink { Id = "id-2", HostKey = host, AppId = appId, State = HostLinkStates.Confirmed, DecidedAt = T1.AddHours(1), Note = null });

        var stored = await _links.GetAsync(host.ToUpperInvariant(), appId);
        Assert.NotNull(stored);
        Assert.Equal("id-1", stored.Id);                     // a linha existente é preservada
        Assert.Equal(host, stored.HostKey);
        Assert.Equal(HostLinkStates.Confirmed, stored.State);
        Assert.Equal(T1.AddHours(1), stored.DecidedAt);
        Assert.Null(stored.Note);
        Assert.Equal(HostLinkOrigins.Human, stored.Origin);
        Assert.Single(await _links.ListAsync(), l => l.HostKey == host);
    }

    [Fact]
    public async Task Repository_Delete_ReportsWhetherSomethingWasRemoved()
    {
        var (appId, _) = await SeedAppAsync(await SeedSquadAsync(), "App");
        var host = $"b-{Marker()}.exemplo.com";
        await _links.UpsertAsync(new HostLink { Id = Guid.NewGuid().ToString("n"), HostKey = host, AppId = appId, State = HostLinkStates.Confirmed, DecidedAt = T1 });

        Assert.True(await _links.DeleteAsync(host, appId));
        Assert.False(await _links.DeleteAsync(host, appId));
        Assert.Null(await _links.GetAsync(host, appId));
    }

    [Fact]
    public async Task Repository_LinksDieWithTheirApp()
    {
        var (appId, _) = await SeedAppAsync(await SeedSquadAsync(), "App");
        var host = $"c-{Marker()}.exemplo.com";
        await _links.UpsertAsync(new HostLink { Id = Guid.NewGuid().ToString("n"), HostKey = host, AppId = appId, State = HostLinkStates.Confirmed, DecidedAt = T1 });

        await _apps.DeleteAllAsync(appId);

        Assert.DoesNotContain(await _links.ListAsync(), l => l.HostKey == host);
    }

    // ---- Serviço: listagem com "visto" ------------------------------------------------------------------------------------

    [Fact]
    public async Task List_SaysWhichLinksAreStillSeenInTheCurrentAnalyses_TemplateIncluded()
    {
        var marker = Marker();
        var (callee, _) = await SeedAppAsync(await SeedSquadAsync(), "Pagamentos");
        var (_, caller) = await SeedAppAsync(await SeedSquadAsync(), "Checkout");
        await AnalyzeAsync(caller, $"pag-{marker}.dev.exemplo.com");

        await Links().SetAsync($"pag-{marker}.{{env}}.exemplo.com", callee, HostLinkStates.Confirmed, null);   // cobre o dev visto
        await Links().SetAsync($"nunca-{marker}.exemplo.com", callee, HostLinkStates.Rejected, null);         // ninguém chama

        var items = (await Links().ListAsync()).Where(i => i.HostKey.Contains(marker)).ToList();

        var template = items.Single(i => i.IsTemplate);
        Assert.True(template.Seen);
        Assert.Equal("Pagamentos", template.AppName);
        Assert.False(items.Single(i => !i.IsTemplate).Seen);   // "não visto na última análise"
    }

    // ---- Leituras da 014 com o vínculo -----------------------------------------------------------------------------------

    [Fact]
    public async Task Hosts_ShowTheEffectiveLink_TheTemplateCoversEachEnvironment_AndLinkStatusFilters()
    {
        var marker = Marker();
        var (callee, _) = await SeedAppAsync(await SeedSquadAsync(), "Pagamentos");
        var (other, _) = await SeedAppAsync(await SeedSquadAsync(), "Faturas");
        var (_, caller) = await SeedAppAsync(await SeedSquadAsync(), "Checkout");
        await AnalyzeAsync(caller,
            $"pag-{marker}.{{env}}.exemplo.com", $"pag-{marker}.dev.exemplo.com", $"pag-{marker}.prod.exemplo.com",
            $"pag-{marker}.qa.exemplo.com",              // fora da lista de valores do {env}
            $"gw-{marker}.exemplo.com", $"livre-{marker}.exemplo.com");

        await Links().SetAsync($"pag-{marker}.{{env}}.exemplo.com", callee, HostLinkStates.Confirmed, "modelo");
        await Links().SetAsync($"pag-{marker}.prod.exemplo.com", other, HostLinkStates.Confirmed, null);        // o exato vence
        await Links().SetAsync($"gw-{marker}.exemplo.com", callee, HostLinkStates.Rejected, null);

        var all = (await Integrations().ListHostsAsync(marker, null)).Hosts.ToDictionary(h => h.Key);

        Assert.Equal(LinkStatuses.Confirmed, all[$"pag-{marker}.{{env}}.exemplo.com"].Link.Status);
        Assert.Equal(HostKeyMatcher.ViaExact, all[$"pag-{marker}.{{env}}.exemplo.com"].Link.Confirmed!.Via);

        var dev = all[$"pag-{marker}.dev.exemplo.com"].Link.Confirmed!;
        Assert.Equal(callee, dev.AppId);
        Assert.Equal(HostKeyMatcher.ViaTemplate, dev.Via);
        Assert.Equal($"pag-{marker}.{{env}}.exemplo.com", dev.Template);

        var prod = all[$"pag-{marker}.prod.exemplo.com"].Link.Confirmed!;
        Assert.Equal(other, prod.AppId);
        Assert.Equal(HostKeyMatcher.ViaExact, prod.Via);

        Assert.Equal(LinkStatuses.None, all[$"pag-{marker}.qa.exemplo.com"].Link.Status);   // nunca por palpite
        Assert.Equal(LinkStatuses.Rejected, all[$"gw-{marker}.exemplo.com"].Link.Status);
        Assert.Equal(LinkStatuses.None, all[$"livre-{marker}.exemplo.com"].Link.Status);

        var pending = await Integrations().ListHostsAsync(marker, null, LinkStatuses.None);
        Assert.Equal([$"livre-{marker}.exemplo.com", $"pag-{marker}.qa.exemplo.com"], pending.Hosts.Select(h => h.Key).Order());
        Assert.Equal(3, (await Integrations().ListHostsAsync(marker, null, LinkStatuses.Confirmed)).Hosts.Count);
        Assert.Single((await Integrations().ListHostsAsync(marker, null, LinkStatuses.Rejected)).Hosts);
        Assert.Equal(6, (await Integrations().ListHostsAsync(marker, null, "")).Hosts.Count);
    }

    [Fact]
    public async Task Hosts_UnknownLinkStatus_IsRefused() =>
        await Assert.ThrowsAsync<ArgumentException>(() => Integrations().ListHostsAsync(null, null, "talvez"));

    [Fact]
    public async Task HostDetail_CarriesTheLink()
    {
        var marker = Marker();
        var (callee, _) = await SeedAppAsync(await SeedSquadAsync(), "Pagamentos");
        var (_, caller) = await SeedAppAsync(await SeedSquadAsync(), "Checkout");
        await AnalyzeAsync(caller, $"d-{marker}.hom.exemplo.com");
        await Links().SetAsync($"d-{marker}.{{env}}.exemplo.com", callee, HostLinkStates.Confirmed, null);

        var detail = await Integrations().GetHostAsync($"d-{marker}.hom.exemplo.com");

        Assert.Equal(callee, detail.Link.Confirmed!.AppId);
        Assert.Equal(HostKeyMatcher.ViaTemplate, detail.Link.Confirmed.Via);
    }

    [Fact]
    public async Task ForApp_AnnotatesEachIntegrationWithItsDestination_OnlyWhenConfirmed()
    {
        var marker = Marker();
        var (callee, _) = await SeedAppAsync(await SeedSquadAsync(), "Pagamentos");
        var (caller, repo) = await SeedAppAsync(await SeedSquadAsync(), "Checkout");
        await AnalyzeAsync(repo, $"pag-{marker}.dev.exemplo.com", $"gw-{marker}.exemplo.com", null);
        await Links().SetAsync($"pag-{marker}.{{env}}.exemplo.com", callee, HostLinkStates.Confirmed, null);
        await Links().SetAsync($"gw-{marker}.exemplo.com", callee, HostLinkStates.Rejected, null);

        var section = Assert.Single(Assert.Single((await Integrations().GetForAppAsync(caller)).Repositories).Sections);

        var dev = section.Integrations.Single(i => i.Host == $"pag-{marker}.dev.exemplo.com");
        Assert.Equal(callee, section.Destinations[dev.Id].AppId);
        Assert.Equal(HostKeyMatcher.ViaTemplate, section.Destinations[dev.Id].Via);
        Assert.Single(section.Destinations);   // o rejeitado e o sem host ficam sem destino
        Assert.DoesNotContain(section.Integrations.Single(i => i.Host == $"gw-{marker}.exemplo.com").Id, section.Destinations.Keys);
    }

    [Fact]
    public async Task Reanalyzing_NeverTouchesTheLinks()
    {
        var marker = Marker();
        var (callee, _) = await SeedAppAsync(await SeedSquadAsync(), "Pagamentos");
        var (_, caller) = await SeedAppAsync(await SeedSquadAsync(), "Checkout");
        await AnalyzeAsync(caller, $"r-{marker}.exemplo.com");
        await Links().SetAsync($"r-{marker}.exemplo.com", callee, HostLinkStates.Confirmed, null);

        await AnalyzeAsync(caller, $"r-{marker}.exemplo.com");   // nova análise do mesmo repositório

        Assert.Equal(HostLinkStates.Confirmed, (await _links.GetAsync($"r-{marker}.exemplo.com", callee))!.State);
    }

    [Fact]
    public async Task ConfiguredEnvValues_ReplaceTheDefaults()
    {
        var marker = Marker();
        _options.EnvPlaceholderValues = ["qa"];
        var (callee, _) = await SeedAppAsync(await SeedSquadAsync(), "Pagamentos");
        var (_, caller) = await SeedAppAsync(await SeedSquadAsync(), "Checkout");
        await AnalyzeAsync(caller, $"q-{marker}.qa.exemplo.com", $"q-{marker}.dev.exemplo.com");
        await Links().SetAsync($"q-{marker}.{{env}}.exemplo.com", callee, HostLinkStates.Confirmed, null);

        var hosts = (await Integrations().ListHostsAsync(marker, null)).Hosts.ToDictionary(h => h.Key);

        Assert.Equal(LinkStatuses.Confirmed, hosts[$"q-{marker}.qa.exemplo.com"].Link.Status);
        Assert.Equal(LinkStatuses.None, hosts[$"q-{marker}.dev.exemplo.com"].Link.Status);   // o padrão foi substituído
    }


    // ---- Host externo (spec 019) com o SQLite de verdade ----------------------------------------------------------------

    [Fact]
    public async Task ExternalRepository_Upsert_KeepsOneRowPerHost_AndDeleteReports()
    {
        var host = $"ext-{Marker()}.outra.com";

        await _externals.UpsertAsync(new HostExternal { Id = "e-1", HostKey = host.ToUpperInvariant(), Name = "antes", DecidedAt = T1 });
        await _externals.UpsertAsync(new HostExternal { Id = "e-2", HostKey = host, Name = null, DecidedAt = T1.AddHours(1), Note = "depois" });

        var stored = Assert.Single(await _externals.ListAsync(), e => e.HostKey == host);
        Assert.Equal("e-1", stored.Id);                    // a linha existente é preservada
        Assert.Null(stored.Name);
        Assert.Equal("depois", stored.Note);
        Assert.Equal(HostLinkOrigins.Human, stored.Origin);
        Assert.Equal(T1.AddHours(1), stored.DecidedAt);

        Assert.True(await _externals.DeleteAsync(host));
        Assert.False(await _externals.DeleteAsync(host));
    }

    [Fact]
    public async Task ExternalHost_LeavesTheQueue_AppearsAsExterno_AndSurvivesReanalysis()
    {
        var marker = Marker();
        var (callee, _) = await SeedAppAsync(await SeedSquadAsync(), "Pagamentos");
        var (_, caller) = await SeedAppAsync(await SeedSquadAsync(), "Checkout");
        await AnalyzeAsync(caller, $"x-{marker}.hom.exemplo.com", $"x-{marker}.prod.exemplo.com", $"livre-{marker}.exemplo.com");

        await Links().SetExternalAsync($"x-{marker}.{{env}}.exemplo.com", "API da squad X", null);     // o modelo cobre hom e prod
        await Links().SetAsync($"livre-{marker}.exemplo.com", callee, HostLinkStates.Confirmed, null);

        var hosts = (await Integrations().ListHostsAsync(marker, null)).Hosts.ToDictionary(h => h.Key);
        var hom = hosts[$"x-{marker}.hom.exemplo.com"].Link;
        Assert.Equal(LinkStatuses.External, hom.Status);
        Assert.Equal("API da squad X", hom.External!.Name);
        Assert.Equal(HostKeyMatcher.ViaTemplate, hom.External.Via);

        Assert.Empty((await Integrations().ListHostsAsync(marker, null, LinkStatuses.None)).Hosts);
        Assert.Equal(2, (await Integrations().ListHostsAsync(marker, null, LinkStatuses.External)).Hosts.Count);

        await AnalyzeAsync(caller, $"x-{marker}.hom.exemplo.com");     // reanálise não apaga a decisão
        Assert.Contains(await _externals.ListAsync(), e => e.HostKey == $"x-{marker}.{{env}}.exemplo.com");
    }

    [Fact]
    public async Task AppIntegrations_CarryTheExternalMark_ButOnlyWhereThereIsNoApp()
    {
        var marker = Marker();
        var (callee, _) = await SeedAppAsync(await SeedSquadAsync(), "Pagamentos");
        var (callerApp, callerRepo) = await SeedAppAsync(await SeedSquadAsync(), "Checkout");
        await AnalyzeAsync(callerRepo, $"ext-{marker}.exemplo.com", $"app-{marker}.exemplo.com", $"nada-{marker}.exemplo.com");

        await Links().SetExternalAsync($"ext-{marker}.exemplo.com", "Terceiro", null);
        await Links().SetAsync($"app-{marker}.exemplo.com", callee, HostLinkStates.Confirmed, null);

        var section = Assert.Single((await Integrations().GetForAppAsync(callerApp)).Repositories.Single().Sections);
        var byHost = section.Integrations.ToDictionary(i => i.Host!);

        Assert.Equal("Terceiro", section.Externals[byHost[$"ext-{marker}.exemplo.com"].Id].Name);
        Assert.DoesNotContain(byHost[$"ext-{marker}.exemplo.com"].Id, section.Destinations.Keys);
        Assert.Equal(callee, section.Destinations[byHost[$"app-{marker}.exemplo.com"].Id].AppId);
        Assert.DoesNotContain(byHost[$"app-{marker}.exemplo.com"].Id, section.Externals.Keys);
        Assert.DoesNotContain(byHost[$"nada-{marker}.exemplo.com"].Id, section.Externals.Keys);
    }
}
