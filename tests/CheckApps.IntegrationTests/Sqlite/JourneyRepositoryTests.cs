using CheckApps.Domain;
using CheckApps.Infrastructure.Sqlite;
using Dapper;
using Microsoft.Data.Sqlite;

namespace CheckApps.IntegrationTests.Sqlite;

/// <summary>A persistência das jornadas (spec 022) sobre SQLite de verdade: campos, tags, passos, cascatas e chaves estrangeiras.</summary>
public class JourneyRepositoryTests(SqliteFixture fixture) : IClassFixture<SqliteFixture>
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly SquadRepository _squads = new(fixture.ConnectionFactory);
    private readonly AppRepository _apps = new(fixture.ConnectionFactory);
    private readonly JourneyRepository _journeys = new(fixture.ConnectionFactory);

    private async Task<(string SquadId, string AppId)> SeedSquadAndAppAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var squadId = $"sq-{suffix}";
        var appId = $"app-{suffix}";
        await _squads.SaveAsync(new Squad { Id = squadId, Name = $"Squad {suffix}" });
        await _apps.SaveAsync(new App { Id = appId, SquadId = squadId, Name = $"App {suffix}" });
        return (squadId, appId);
    }

    private static Journey Journey(string? id = null, string? parent = null, string? squad = null, params string[] tags) =>
        new()
        {
            Id = id ?? $"j-{Guid.NewGuid():N}", Name = "Compra de Fundos", Description = "descrição", Level = "jornada", SquadId = squad,
            ParentId = parent, Tags = tags, CreatedAt = T0, UpdatedAt = T0.AddMinutes(5)
        };

    private static JourneyStep Step(string journeyId, string appId, int position, string path = "/fundos/{id}") =>
        new()
        {
            Id = Guid.NewGuid().ToString("n"), JourneyId = journeyId, Position = position, AppId = appId, Method = "GET", Path = path,
            PathKey = "/fundos/{}", Label = $"passo {position}", Note = "nota"
        };

    [Fact]
    public async Task TryCreate_StoresEveryFieldAndTheTags_AndReadsThemBack()
    {
        var (squadId, _) = await SeedSquadAndAppAsync();
        var journey = Journey(squad: squadId, tags: ["tipo-produto:renda-fixa", "a"]);

        Assert.True(await _journeys.TryCreateAsync(journey));

        var read = (await _journeys.GetAsync(journey.Id))!;
        Assert.Equal(journey with { Tags = [] }, read with { Tags = [] });
        Assert.Equal(["a", "tipo-produto:renda-fixa"], read.Tags);
        Assert.Contains(await _journeys.ListAsync(), j => j.Id == journey.Id && j.Tags.Count == 2);
    }

    [Fact]
    public async Task TryCreate_AnExistingId_ReturnsFalse_AndKeepsTheFirst()
    {
        var first = Journey();
        Assert.True(await _journeys.TryCreateAsync(first));

        Assert.False(await _journeys.TryCreateAsync(first with { Name = "Outra" }));

        Assert.Equal("Compra de Fundos", (await _journeys.GetAsync(first.Id))!.Name);
    }

    [Fact]
    public async Task Get_AJourneyThatDoesNotExist_IsNull() => Assert.Null(await _journeys.GetAsync("nao-existe"));

    [Fact]
    public async Task Update_ChangesTheFieldsAndReplacesTheTags_WithoutTouchingTheIdSourceOrCreationDate()
    {
        var (squadId, _) = await SeedSquadAndAppAsync();
        var journey = Journey(tags: ["velha"]);
        await _journeys.TryCreateAsync(journey);

        await _journeys.UpdateAsync(journey with
        {
            Name = "Nova", Level = null, SquadId = squadId, Tags = ["nova-1", "nova-2"], UpdatedAt = T0.AddDays(1),
            Source = "OUTRA", CreatedAt = T0.AddYears(1)
        });

        var read = (await _journeys.GetAsync(journey.Id))!;
        Assert.Equal("Nova", read.Name);
        Assert.Null(read.Level);
        Assert.Equal(squadId, read.SquadId);
        Assert.Equal(["nova-1", "nova-2"], read.Tags);
        Assert.Equal(T0.AddDays(1), read.UpdatedAt);
        Assert.Equal(JourneySources.Manual, read.Source);
        Assert.Equal(T0, read.CreatedAt);
    }

    [Fact]
    public async Task Steps_ComeBackInOrderOfPosition_AndReplaceSwapsTheWholeList()
    {
        var (_, appId) = await SeedSquadAndAppAsync();
        var journey = Journey();
        await _journeys.TryCreateAsync(journey);

        await _journeys.ReplaceStepsAsync(journey.Id, [Step(journey.Id, appId, 2), Step(journey.Id, appId, 0), Step(journey.Id, appId, 1)]);
        Assert.Equal([0, 1, 2], (await _journeys.GetStepsAsync(journey.Id)).Select(s => s.Position));

        await _journeys.ReplaceStepsAsync(journey.Id, [Step(journey.Id, appId, 0, "/novo")]);
        var steps = await _journeys.GetStepsAsync(journey.Id);
        Assert.Equal("/novo", Assert.Single(steps).Path);
        Assert.Equal("/fundos/{}", steps[0].PathKey);
        Assert.Equal("nota", steps[0].Note);
    }

    [Fact]
    public async Task ReplaceSteps_ThatFailsHalfway_RollsBackAndKeepsTheOldList()
    {
        var (_, appId) = await SeedSquadAndAppAsync();
        var journey = Journey();
        await _journeys.TryCreateAsync(journey);
        await _journeys.ReplaceStepsAsync(journey.Id, [Step(journey.Id, appId, 0, "/antigo")]);

        // Mesma posição duas vezes: o índice único recusa a segunda linha, depois de a primeira já ter entrado.
        await Assert.ThrowsAsync<SqliteException>(() =>
            _journeys.ReplaceStepsAsync(journey.Id, [Step(journey.Id, appId, 0, "/a"), Step(journey.Id, appId, 0, "/b")]));

        Assert.Equal("/antigo", Assert.Single(await _journeys.GetStepsAsync(journey.Id)).Path);
    }

    [Fact]
    public async Task ListAllSteps_ReturnsTheStepsOfEveryJourney()
    {
        var (_, appId) = await SeedSquadAndAppAsync();
        var one = Journey();
        var two = Journey();
        await _journeys.TryCreateAsync(one);
        await _journeys.TryCreateAsync(two);
        await _journeys.ReplaceStepsAsync(one.Id, [Step(one.Id, appId, 0)]);
        await _journeys.ReplaceStepsAsync(two.Id, [Step(two.Id, appId, 0), Step(two.Id, appId, 1)]);

        var all = await _journeys.ListAllStepsAsync();

        Assert.Equal(1, all.Count(s => s.JourneyId == one.Id));
        Assert.Equal(2, all.Count(s => s.JourneyId == two.Id));
    }

    [Fact]
    public async Task Delete_CascadesToTheStepsAndTheTags()
    {
        var (_, appId) = await SeedSquadAndAppAsync();
        var journey = Journey(tags: ["t"]);
        await _journeys.TryCreateAsync(journey);
        await _journeys.ReplaceStepsAsync(journey.Id, [Step(journey.Id, appId, 0)]);

        await _journeys.DeleteAsync(journey.Id);

        Assert.Null(await _journeys.GetAsync(journey.Id));
        using var connection = fixture.ConnectionFactory.Create();
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM journey_steps WHERE journey_id = @id", new { id = journey.Id }));
        Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM journey_tags WHERE journey_id = @id", new { id = journey.Id }));
    }

    [Fact]
    public async Task TheParentForeignKey_RefusesToDeleteAParentThatStillHasChildren()
    {
        var parent = Journey();
        var child = Journey(parent: parent.Id);
        await _journeys.TryCreateAsync(parent);
        await _journeys.TryCreateAsync(child);

        await Assert.ThrowsAsync<SqliteException>(() => _journeys.DeleteAsync(parent.Id));

        Assert.NotNull(await _journeys.GetAsync(parent.Id));
    }

    [Fact]
    public async Task AJourneyWithAnUnknownParentSquadOrApp_IsRefusedByTheForeignKeys()
    {
        await Assert.ThrowsAsync<SqliteException>(() => _journeys.TryCreateAsync(Journey(parent: "nao-existe")));
        await Assert.ThrowsAsync<SqliteException>(() => _journeys.TryCreateAsync(Journey(squad: "nao-existe")));

        var journey = Journey();
        await _journeys.TryCreateAsync(journey);
        await Assert.ThrowsAsync<SqliteException>(() => _journeys.ReplaceStepsAsync(journey.Id, [Step(journey.Id, "app-nao-existe", 0)]));
    }

    [Fact]
    public async Task CountOwnedBySquad_AndAnyStepUsesApp_FeedTheRemovalGuards()
    {
        var (squadId, appId) = await SeedSquadAndAppAsync();
        Assert.Equal(0, await _journeys.CountOwnedBySquadAsync(squadId));
        Assert.False(await _journeys.AnyStepUsesAppAsync(appId));

        var journey = Journey(squad: squadId);
        await _journeys.TryCreateAsync(journey);
        await _journeys.ReplaceStepsAsync(journey.Id, [Step(journey.Id, appId, 0)]);

        Assert.Equal(1, await _journeys.CountOwnedBySquadAsync(squadId));
        Assert.True(await _journeys.AnyStepUsesAppAsync(appId));
    }

    [Fact]
    public async Task TheForeignKeys_AreTheSafetyNet_WhenAnAppOrSquadIsDeletedBehindTheServicesBack()
    {
        var (squadId, appId) = await SeedSquadAndAppAsync();
        var journey = Journey(squad: squadId);
        await _journeys.TryCreateAsync(journey);
        await _journeys.ReplaceStepsAsync(journey.Id, [Step(journey.Id, appId, 0)]);

        using var connection = fixture.ConnectionFactory.Create();
        await Assert.ThrowsAsync<SqliteException>(() => connection.ExecuteAsync("DELETE FROM apps WHERE id = @appId", new { appId }));
        await Assert.ThrowsAsync<SqliteException>(() => connection.ExecuteAsync("DELETE FROM squads WHERE id = @squadId", new { squadId }));
    }
}
