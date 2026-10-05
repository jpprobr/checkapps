using CheckApps.Domain;
using CheckApps.Domain.QualityAssessment;
using CheckApps.Infrastructure.Sqlite;

namespace CheckApps.IntegrationTests.Sqlite;

public class RepositoriesTests(SqliteFixture fixture) : IClassFixture<SqliteFixture>
{
    private readonly SquadRepository _squads = new(fixture.ConnectionFactory);
    private readonly AppRepository _apps = new(fixture.ConnectionFactory);
    private readonly QualitySnapshotRepository _snapshots = new(fixture.ConnectionFactory);
    private readonly IQualityAssessmentEngine _engine = new QualityAssessmentEngineV1();

    [Fact]
    public async Task ListBySquad_ReturnsOnlyAppsOfThatSquad_WithCurrentStateDenormalized()
    {
        var squadId = $"sq-{Guid.NewGuid():N}";
        var otherSquadId = $"sq-{Guid.NewGuid():N}";
        await _squads.SaveAsync(new Squad { Id = squadId, Name = "Squad Teste" });
        await _squads.SaveAsync(new Squad { Id = otherSquadId, Name = "Outra Squad" });

        var appId = await SeedAppWithSnapshotAsync(squadId, coverage: 85, reliability: "A", maintainability: "A", security: "A");
        await SeedAppWithSnapshotAsync(otherSquadId, coverage: 85, reliability: "A", maintainability: "A", security: "A");

        var apps = await _apps.ListBySquadAsync(squadId);

        var app = Assert.Single(apps);
        Assert.Equal(appId, app.Id);
        Assert.Equal(QualityLevel.A, app.CurrentQualityLevel);
        Assert.NotNull(app.CurrentMetrics);
    }

    [Fact]
    public async Task ListByQualityLevel_ReturnsOnlyAppsAtThatLevel()
    {
        var squadId = $"sq-{Guid.NewGuid():N}";
        await _squads.SaveAsync(new Squad { Id = squadId, Name = "Squad Teste" });

        var goodAppId = await SeedAppWithSnapshotAsync(squadId, coverage: 85, reliability: "A", maintainability: "A", security: "A");
        await SeedAppWithSnapshotAsync(squadId, coverage: 40, reliability: "D", maintainability: "D", security: "D");

        var appsAtA = await _apps.ListByQualityLevelAsync(QualityLevel.A);

        Assert.Contains(appsAtA, a => a.Id == goodAppId);
        Assert.All(appsAtA, a => Assert.Equal(QualityLevel.A, a.CurrentQualityLevel));
    }

    [Fact]
    public async Task GetHistory_WithDateRange_ReturnsOnlySnapshotsInRange_OrderedByTime()
    {
        var appId = $"app-{Guid.NewGuid():N}";
        var squadId = $"sq-{Guid.NewGuid():N}";
        await _squads.SaveAsync(new Squad { Id = squadId, Name = "Squad Teste" });
        await _apps.SaveAsync(new App { Id = appId, SquadId = squadId, Name = "app-teste" });

        var jun = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var jul = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var aug = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

        await AddSnapshotAsync(appId, jun, coverage: 70, reliability: "B", maintainability: "A", security: "A");
        await AddSnapshotAsync(appId, jul, coverage: 72, reliability: "B", maintainability: "A", security: "A");
        await AddSnapshotAsync(appId, aug, coverage: 90, reliability: "A", maintainability: "A", security: "A");

        var history = await _snapshots.GetHistoryAsync(appId, from: jul, to: aug);

        Assert.Equal(2, history.Count);
        Assert.Equal(jul, history[0].CollectedAt);
        Assert.Equal(aug, history[1].CollectedAt);
    }

    [Fact]
    public async Task AddSnapshot_UpdatesAppCurrentState_Transactionally()
    {
        var appId = $"app-{Guid.NewGuid():N}";
        var squadId = $"sq-{Guid.NewGuid():N}";
        await _squads.SaveAsync(new Squad { Id = squadId, Name = "Squad Teste" });
        await _apps.SaveAsync(new App { Id = appId, SquadId = squadId, Name = "app-teste" });

        await AddSnapshotAsync(appId, new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            coverage: 40, reliability: "D", maintainability: "D", security: "D");

        var afterFirst = await _apps.GetAsync(appId);
        Assert.Equal(QualityLevel.E, afterFirst!.CurrentQualityLevel);

        await AddSnapshotAsync(appId, new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            coverage: 90, reliability: "A", maintainability: "A", security: "A");

        var afterSecond = await _apps.GetAsync(appId);
        Assert.Equal(QualityLevel.A, afterSecond!.CurrentQualityLevel);
    }

    [Fact]
    public async Task AddSnapshot_OutOfOrder_DoesNotRegressCurrentState()
    {
        var appId = $"app-{Guid.NewGuid():N}";
        var squadId = $"sq-{Guid.NewGuid():N}";
        await _squads.SaveAsync(new Squad { Id = squadId, Name = "Squad Teste" });
        await _apps.SaveAsync(new App { Id = appId, SquadId = squadId, Name = "app-teste" });

        await AddSnapshotAsync(appId, new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            coverage: 90, reliability: "A", maintainability: "A", security: "A");

        // backfill de um mês anterior, registrado depois do de agosto
        await AddSnapshotAsync(appId, new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            coverage: 40, reliability: "D", maintainability: "D", security: "D");

        var app = await _apps.GetAsync(appId);
        Assert.Equal(QualityLevel.A, app!.CurrentQualityLevel);

        var history = await _snapshots.GetHistoryAsync(appId);
        Assert.Equal(2, history.Count);
    }

    private async Task<string> SeedAppWithSnapshotAsync(
        string squadId, double coverage, string reliability, string maintainability, string security)
    {
        var appId = $"app-{Guid.NewGuid():N}";
        await _apps.SaveAsync(new App { Id = appId, SquadId = squadId, Name = appId });
        await AddSnapshotAsync(appId, DateTimeOffset.UtcNow, coverage, reliability, maintainability, security);
        return appId;
    }

    private async Task AddSnapshotAsync(
        string appId, DateTimeOffset collectedAt, double coverage, string reliability, string maintainability, string security)
    {
        var metrics = new QualityMetrics
        {
            Coverage = coverage,
            Bugs = 0,
            Vulnerabilities = 0,
            CodeSmells = 0,
            Duplications = 5,
            Reliability = reliability,
            Maintainability = maintainability,
            Security = security
        };

        await _snapshots.AddAsync(new QualitySnapshot
        {
            Id = $"{appId}-snap-{collectedAt:yyyyMMdd}",
            AppId = appId,
            SonarProjectId = "sonar-1",
            CollectedAt = collectedAt,
            Source = "SONAR_MOCK",
            Metrics = metrics,
            QualityLevel = _engine.Assess(metrics),
            AssessmentRulesVersion = _engine.RulesVersion
        });
    }
}
