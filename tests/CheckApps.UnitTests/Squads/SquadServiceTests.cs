using CheckApps.Application;
using CheckApps.Application.Squads;
using CheckApps.Domain;
using CheckApps.UnitTests.TestDoubles;

namespace CheckApps.UnitTests.Squads;

public class SquadServiceTests
{
    private static readonly DateTimeOffset Jun = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Jul = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Aug = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetQualityEvolution_ClassifiesEachAppTrend_ImprovedWorsenedStableNew()
    {
        var apps = new FakeAppRepository();
        var snapshots = new FakeQualitySnapshotRepository();

        apps.Add(new App { Id = "app-improving", SquadId = "sq-1", Name = "improving" });
        snapshots.Add("app-improving", Jul, QualityLevel.B);
        snapshots.Add("app-improving", Aug, QualityLevel.A);

        apps.Add(new App { Id = "app-worsening", SquadId = "sq-1", Name = "worsening" });
        snapshots.Add("app-worsening", Jul, QualityLevel.B);
        snapshots.Add("app-worsening", Aug, QualityLevel.C);

        apps.Add(new App { Id = "app-stable", SquadId = "sq-1", Name = "stable" });
        snapshots.Add("app-stable", Jul, QualityLevel.A);
        snapshots.Add("app-stable", Aug, QualityLevel.A);

        apps.Add(new App { Id = "app-new", SquadId = "sq-1", Name = "new" });
        snapshots.Add("app-new", Aug, QualityLevel.B);

        var service = new SquadService(new FakeSquadRepository(), apps, snapshots);

        var evolution = await service.GetQualityEvolutionAsync("sq-1");

        Assert.Equal(QualityTrendDirection.Improved, Trend(evolution, "app-improving"));
        Assert.Equal(QualityTrendDirection.Worsened, Trend(evolution, "app-worsening"));
        Assert.Equal(QualityTrendDirection.Stable, Trend(evolution, "app-stable"));
        Assert.Equal(QualityTrendDirection.New, Trend(evolution, "app-new"));
    }

    [Fact]
    public async Task GetQualityEvolution_GroupsLevelCounts_ByCollectionDate()
    {
        var apps = new FakeAppRepository();
        var snapshots = new FakeQualitySnapshotRepository();

        apps.Add(new App { Id = "app-1", SquadId = "sq-1", Name = "a1" });
        apps.Add(new App { Id = "app-2", SquadId = "sq-1", Name = "a2" });
        snapshots.Add("app-1", Jun, QualityLevel.A);
        snapshots.Add("app-2", Jun, QualityLevel.B);

        var service = new SquadService(new FakeSquadRepository(), apps, snapshots);

        var evolution = await service.GetQualityEvolutionAsync("sq-1");

        var point = Assert.Single(evolution.Points);
        Assert.Equal(Jun, point.Period);
        Assert.Equal(1, point.LevelCounts[QualityLevel.A]);
        Assert.Equal(1, point.LevelCounts[QualityLevel.B]);
    }

    [Fact]
    public async Task CreateAsync_GeneratesSlugId_AndDefaultsStatusToActive()
    {
        var service = new SquadService(new FakeSquadRepository(), new FakeAppRepository(), new FakeQualitySnapshotRepository());

        var squad = await service.CreateAsync("Squad Pagamentos", "descrição");

        Assert.Equal("squad-pagamentos", squad.Id);
        Assert.Equal("active", squad.Status);
    }

    [Fact]
    public async Task CreateAsync_OnSlugCollision_AppendsNumericSuffix()
    {
        var squads = new FakeSquadRepository();
        var service = new SquadService(squads, new FakeAppRepository(), new FakeQualitySnapshotRepository());
        await squads.TryCreateAsync(new Squad { Id = "squad-pagamentos", Name = "Existente" });

        var squad = await service.CreateAsync("Squad Pagamentos", null);

        Assert.Equal("squad-pagamentos-2", squad.Id);
    }

    [Fact]
    public async Task UpdateAsync_ChangesFields_WithoutLosingId()
    {
        var squads = new FakeSquadRepository();
        await squads.SaveAsync(new Squad { Id = "sq-1", Name = "Antigo", Status = "active" });
        var service = new SquadService(squads, new FakeAppRepository(), new FakeQualitySnapshotRepository());

        var updated = await service.UpdateAsync("sq-1", "Novo Nome", "nova descrição", "inactive");

        Assert.Equal("sq-1", updated.Id);
        Assert.Equal("Novo Nome", updated.Name);
        Assert.Equal("inactive", updated.Status);
    }

    [Fact]
    public async Task DeleteAsync_Blocked_WhenSquadHasApps()
    {
        var squads = new FakeSquadRepository();
        await squads.SaveAsync(new Squad { Id = "sq-1", Name = "Squad" });
        var apps = new FakeAppRepository();
        apps.Add(new App { Id = "app-1", SquadId = "sq-1", Name = "app" });
        var service = new SquadService(squads, apps, new FakeQualitySnapshotRepository());

        await Assert.ThrowsAsync<DomainConflictException>(() => service.DeleteAsync("sq-1"));
        Assert.NotNull(await squads.GetAsync("sq-1"));
    }

    [Fact]
    public async Task DeleteAsync_Succeeds_WhenSquadHasNoApps()
    {
        var squads = new FakeSquadRepository();
        await squads.SaveAsync(new Squad { Id = "sq-1", Name = "Squad" });
        var service = new SquadService(squads, new FakeAppRepository(), new FakeQualitySnapshotRepository());

        await service.DeleteAsync("sq-1");

        Assert.Null(await squads.GetAsync("sq-1"));
    }

    private static QualityTrendDirection Trend(SquadQualityEvolution evolution, string appId) =>
        evolution.AppTrends.Single(t => t.AppId == appId).Direction;
}
