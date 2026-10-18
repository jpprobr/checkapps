using CheckApps.Application;
using CheckApps.Application.Apps;
using CheckApps.Application.Checks;
using CheckApps.Domain;
using CheckApps.UnitTests.TestDoubles;

namespace CheckApps.UnitTests.Apps;

public class AppServiceTests
{
    [Fact]
    public async Task CreateAsync_GeneratesSlugId_AndRequiresExistingSquad()
    {
        var squads = new FakeSquadRepository();
        await squads.SaveAsync(new Squad { Id = "sq-1", Name = "Squad" });
        var service = new AppService(new FakeAppRepository(), squads, new FakeQualitySnapshotRepository(), new FakeCheckRepository());

        var app = await service.CreateAsync("Checkout API", "sq-1", null, "high", ["demo"], null);

        Assert.Equal("checkout-api", app.Id);
        Assert.Equal("sq-1", app.SquadId);
        Assert.Equal("active", app.Status);
    }

    [Fact]
    public async Task CreateAsync_ThrowsKeyNotFound_WhenSquadDoesNotExist()
    {
        var service = new AppService(new FakeAppRepository(), new FakeSquadRepository(), new FakeQualitySnapshotRepository(), new FakeCheckRepository());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreateAsync("App", "sq-inexistente", null, null, null, null));
    }

    [Fact]
    public async Task UpdateAsync_CanMoveAppToAnotherSquad_AndPreservesCurrentQualityState()
    {
        var squads = new FakeSquadRepository();
        await squads.SaveAsync(new Squad { Id = "sq-1", Name = "Origem" });
        await squads.SaveAsync(new Squad { Id = "sq-2", Name = "Destino" });

        var apps = new FakeAppRepository();
        var metrics = new QualityMetrics
        {
            Coverage = 90, Bugs = 0, Vulnerabilities = 0, CodeSmells = 0, Duplications = 2,
            Reliability = "A", Maintainability = "A", Security = "A"
        };
        apps.Add(new App
        {
            Id = "app-1",
            SquadId = "sq-1",
            Name = "App",
            CurrentQualityLevel = QualityLevel.A,
            CurrentMetrics = metrics,
            LastSnapshotAt = DateTimeOffset.UtcNow
        });

        var service = new AppService(apps, squads, new FakeQualitySnapshotRepository(), new FakeCheckRepository());

        var updated = await service.UpdateAsync("app-1", "App Renomeado", "sq-2", null, null, null, "active", null);

        Assert.Equal("sq-2", updated.SquadId);
        Assert.Equal("App Renomeado", updated.Name);
        Assert.Equal(QualityLevel.A, updated.CurrentQualityLevel);
        Assert.NotNull(updated.CurrentMetrics);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsKeyNotFound_WhenMovingToNonExistentSquad()
    {
        var squads = new FakeSquadRepository();
        await squads.SaveAsync(new Squad { Id = "sq-1", Name = "Origem" });
        var apps = new FakeAppRepository();
        apps.Add(new App { Id = "app-1", SquadId = "sq-1", Name = "App" });
        var service = new AppService(apps, squads, new FakeQualitySnapshotRepository(), new FakeCheckRepository());

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.UpdateAsync("app-1", "App", "sq-inexistente", null, null, null, "active", null));
    }

    [Fact]
    public async Task DeleteAsync_Blocked_WhenAppHasQualitySnapshots()
    {
        var apps = new FakeAppRepository();
        apps.Add(new App { Id = "app-1", SquadId = "sq-1", Name = "App" });
        var snapshots = new FakeQualitySnapshotRepository();
        snapshots.Add("app-1", DateTimeOffset.UtcNow, QualityLevel.A);
        var service = new AppService(apps, new FakeSquadRepository(), snapshots, new FakeCheckRepository());

        await Assert.ThrowsAsync<DomainConflictException>(() => service.DeleteAsync("app-1"));
        Assert.NotNull(await apps.GetAsync("app-1"));
    }

    [Fact]
    public async Task DeleteAsync_Blocked_WhenAppIsInACheck()
    {
        var apps = new FakeAppRepository();
        apps.Add(new App { Id = "app-1", SquadId = "sq-1", Name = "App" });
        var checks = new FakeCheckRepository();
        var checkService = new CheckService(checks, apps, TimeProvider.System);
        var check = await checkService.CreateAsync(new CheckFields("Atualizar .NET 10", null, null, null, null, null));
        await checkService.AddAppsAsync(check.Id, ["app-1"]);
        var service = new AppService(apps, new FakeSquadRepository(), new FakeQualitySnapshotRepository(), checks);

        var conflict = await Assert.ThrowsAsync<DomainConflictException>(() => service.DeleteAsync("app-1"));
        Assert.Contains("checagens", conflict.Message);
        Assert.NotNull(await apps.GetAsync("app-1"));
    }

    [Fact]
    public async Task DeleteAsync_Succeeds_WhenAppHasNoQualitySnapshots()
    {
        var apps = new FakeAppRepository();
        apps.Add(new App { Id = "app-1", SquadId = "sq-1", Name = "App" });
        var service = new AppService(apps, new FakeSquadRepository(), new FakeQualitySnapshotRepository(), new FakeCheckRepository());

        await service.DeleteAsync("app-1");

        Assert.Null(await apps.GetAsync("app-1"));
    }

    [Fact]
    public async Task Links_CanBeAddedUpdatedAndRemoved()
    {
        var apps = new FakeAppRepository();
        apps.Add(new App { Id = "app-1", SquadId = "sq-1", Name = "App" });
        var service = new AppService(apps, new FakeSquadRepository(), new FakeQualitySnapshotRepository(), new FakeCheckRepository());

        var link = await service.AddLinkAsync("app-1", "Confluence", "https://wiki.example.com");
        Assert.Single(await service.ListLinksAsync("app-1"));

        await service.UpdateLinkAsync("app-1", link.Id, "Confluence (atualizado)", "https://wiki.example.com/v2");
        var afterUpdate = await service.ListLinksAsync("app-1");
        Assert.Equal("Confluence (atualizado)", afterUpdate.Single().Description);

        await service.DeleteLinkAsync("app-1", link.Id);
        Assert.Empty(await service.ListLinksAsync("app-1"));
    }

    [Fact]
    public async Task AddAndUpdateLink_ThrowKeyNotFound_WhenAppDoesNotExist()
    {
        var service = new AppService(new FakeAppRepository(), new FakeSquadRepository(), new FakeQualitySnapshotRepository(), new FakeCheckRepository());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AddLinkAsync("nope", "Wiki", "https://wiki.example.com"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdateLinkAsync("nope", "l1", "Wiki", "https://wiki.example.com"));
    }
}
