using CheckApps.Domain;
using CheckApps.Domain.QualityAssessment;
using CheckApps.Infrastructure.Sqlite;

namespace CheckApps.IntegrationTests.Sqlite;

public class ManagementTests(SqliteFixture fixture) : IClassFixture<SqliteFixture>
{
    private readonly SquadRepository _squads = new(fixture.ConnectionFactory);
    private readonly AppRepository _apps = new(fixture.ConnectionFactory);
    private readonly QualitySnapshotRepository _snapshots = new(fixture.ConnectionFactory);
    private readonly IQualityAssessmentEngine _engine = new QualityAssessmentEngineV1();

    [Fact]
    public async Task TryCreateSquad_SucceedsOnce_FailsOnDuplicateId()
    {
        var squadId = $"sq-{Guid.NewGuid():N}";

        var firstAttempt = await _squads.TryCreateAsync(new Squad { Id = squadId, Name = "Squad Teste" });
        var secondAttempt = await _squads.TryCreateAsync(new Squad { Id = squadId, Name = "Outro Nome" });

        Assert.True(firstAttempt);
        Assert.False(secondAttempt);

        var stored = await _squads.GetAsync(squadId);
        Assert.Equal("Squad Teste", stored!.Name);
    }

    [Fact]
    public async Task TryCreateApp_SucceedsOnce_FailsOnDuplicateId()
    {
        var squadId = $"sq-{Guid.NewGuid():N}";
        var appId = $"app-{Guid.NewGuid():N}";
        await _squads.SaveAsync(new Squad { Id = squadId, Name = "Squad Teste" });

        var firstAttempt = await _apps.TryCreateAsync(new App { Id = appId, SquadId = squadId, Name = "App Teste" });
        var secondAttempt = await _apps.TryCreateAsync(new App { Id = appId, SquadId = squadId, Name = "Outro Nome" });

        Assert.True(firstAttempt);
        Assert.False(secondAttempt);
    }

    [Fact]
    public async Task DeleteSquad_RemovesTheItem()
    {
        var squadId = $"sq-{Guid.NewGuid():N}";
        await _squads.SaveAsync(new Squad { Id = squadId, Name = "Squad Teste" });

        await _squads.DeleteAsync(squadId);

        Assert.Null(await _squads.GetAsync(squadId));
    }

    [Fact]
    public async Task DeleteAllApp_RemovesMetadataRepositoriesSonarProjectsAndLinks()
    {
        var squadId = $"sq-{Guid.NewGuid():N}";
        var appId = $"app-{Guid.NewGuid():N}";
        await _squads.SaveAsync(new Squad { Id = squadId, Name = "Squad Teste" });
        await _apps.SaveAsync(new App { Id = appId, SquadId = squadId, Name = "App Teste" });
        await _apps.AddRepositoryAsync(new Repository { Id = "repo-1", AppId = appId, Name = "repo" });
        await _apps.AddSonarProjectAsync(new SonarProject { Id = "sonar-1", AppId = appId, ProjectKey = "key", Source = "SONAR_MOCK" });
        await _apps.SaveLinkAsync(new AppLink { Id = "link-1", AppId = appId, Description = "Runbook", Uri = "https://example.com" });

        await _apps.DeleteAllAsync(appId);

        Assert.Null(await _apps.GetAsync(appId));
        Assert.Empty(await _apps.GetRepositoriesAsync(appId));
        Assert.Empty(await _apps.GetSonarProjectsAsync(appId));
        Assert.Empty(await _apps.GetLinksAsync(appId));
    }

    [Fact]
    public async Task AppLinks_CanBeAddedUpdatedAndRemoved()
    {
        var squadId = $"sq-{Guid.NewGuid():N}";
        var appId = $"app-{Guid.NewGuid():N}";
        var linkId = $"link-{Guid.NewGuid():N}";
        await _squads.SaveAsync(new Squad { Id = squadId, Name = "Squad Teste" });
        await _apps.SaveAsync(new App { Id = appId, SquadId = squadId, Name = "App Teste" });

        await _apps.SaveLinkAsync(new AppLink { Id = linkId, AppId = appId, Description = "Confluence", Uri = "https://wiki.example.com" });
        var afterCreate = await _apps.GetLinksAsync(appId);
        Assert.Single(afterCreate);
        Assert.Equal("Confluence", afterCreate[0].Description);

        await _apps.SaveLinkAsync(new AppLink { Id = linkId, AppId = appId, Description = "Confluence (atualizado)", Uri = "https://wiki.example.com/v2" });
        var afterUpdate = await _apps.GetLinksAsync(appId);
        Assert.Single(afterUpdate);
        Assert.Equal("Confluence (atualizado)", afterUpdate[0].Description);

        await _apps.DeleteLinkAsync(appId, linkId);
        Assert.Empty(await _apps.GetLinksAsync(appId));
    }

    [Fact]
    public async Task DeleteAllAsync_HasNoBuiltInGuard_AlsoWipesQualitySnapshots()
    {
        // DeleteAllAsync é um primitivo "burro": apaga a partição inteira, incluindo
        // SNAPSHOT#. A regra "não remover app com histórico" é responsabilidade do
        // AppService (camada de Application), que só chama DeleteAllAsync depois de
        // confirmar que não há snapshots — este teste documenta que o repositório,
        // sozinho, não oferece essa proteção.
        var squadId = $"sq-{Guid.NewGuid():N}";
        var appId = $"app-{Guid.NewGuid():N}";
        await _squads.SaveAsync(new Squad { Id = squadId, Name = "Squad Teste" });
        await _apps.SaveAsync(new App { Id = appId, SquadId = squadId, Name = "App Teste" });

        var metrics = new QualityMetrics
        {
            Coverage = 80, Bugs = 0, Vulnerabilities = 0, CodeSmells = 0, Duplications = 5,
            Reliability = "A", Maintainability = "A", Security = "A"
        };
        await _snapshots.AddAsync(new QualitySnapshot
        {
            Id = "snap-1",
            AppId = appId,
            SonarProjectId = "sonar-1",
            CollectedAt = DateTimeOffset.UtcNow,
            Source = "TEST",
            Metrics = metrics,
            QualityLevel = _engine.Assess(metrics),
            AssessmentRulesVersion = _engine.RulesVersion
        });

        await _apps.DeleteAllAsync(appId);

        Assert.Null(await _apps.GetAsync(appId));
        Assert.Null(await _snapshots.GetLatestAsync(appId));
    }
}
