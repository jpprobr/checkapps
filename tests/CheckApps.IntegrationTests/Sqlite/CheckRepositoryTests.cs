using CheckApps.Domain;
using CheckApps.Domain.Checks;
using CheckApps.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;

namespace CheckApps.IntegrationTests.Sqlite;

public class CheckRepositoryTests(SqliteFixture fixture) : IClassFixture<SqliteFixture>
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SquadRepository _squads = new(fixture.ConnectionFactory);
    private readonly AppRepository _apps = new(fixture.ConnectionFactory);
    private readonly CheckRepository _checks = new(fixture.ConnectionFactory);

    [Fact]
    public async Task TryCreate_RoundTripsAllFields_AndFailsOnDuplicateId()
    {
        var check = NewCheck();
        check.Description = "Descrição";
        check.Category = "segurança";
        check.Author = "Maria";
        check.StartDate = new DateOnly(2026, 10, 1);
        check.EndDate = new DateOnly(2026, 12, 31);

        Assert.True(await _checks.TryCreateAsync(check));
        Assert.False(await _checks.TryCreateAsync(NewCheck(check.Id)));

        var stored = await _checks.GetAsync(check.Id);
        Assert.NotNull(stored);
        Assert.Equal(check.Title, stored.Title);
        Assert.Equal("segurança", stored.Category);
        Assert.Equal("Maria", stored.Author);
        Assert.Equal(new DateOnly(2026, 10, 1), stored.StartDate);
        Assert.Equal(new DateOnly(2026, 12, 31), stored.EndDate);
        Assert.Equal(CheckStatus.Open, stored.Status);
        Assert.Equal(T0, stored.CreatedAt);
        Assert.Null(stored.ClosedAt);
    }

    [Fact]
    public async Task List_HidesClosedByDefault_AndCountsItemsByStatus()
    {
        var (_, appIds) = await SeedAppsAsync(3);
        var open = await CreateCheckAsync();
        var closed = await CreateCheckAsync();
        foreach (var appId in appIds)
        {
            await AddAsync(open.Id, appId);
        }
        await UpdateAsync(open.Id, appIds[0], CheckItemStatus.Conforme);
        await UpdateAsync(open.Id, appIds[1], CheckItemStatus.EmAndamento);

        closed.Status = CheckStatus.Closed;
        closed.ClosedAt = T0.AddDays(1);
        await _checks.SetStatusAsync(closed, Event(closed.Id, CheckEventType.Closed));

        var openOnly = await _checks.ListAsync(includeClosed: false);
        var all = await _checks.ListAsync(includeClosed: true);

        Assert.Contains(openOnly, s => s.Check.Id == open.Id);
        Assert.DoesNotContain(openOnly, s => s.Check.Id == closed.Id);
        Assert.Contains(all, s => s.Check.Id == closed.Id);

        var summary = openOnly.Single(s => s.Check.Id == open.Id);
        Assert.Equal(3, summary.Progress.Total);
        Assert.Equal(1, summary.Progress.Done);
        Assert.Equal(1, summary.Progress.CountsByStatus[CheckItemStatus.EmAndamento]);
        Assert.Equal(1, summary.Progress.CountsByStatus[CheckItemStatus.Pendente]);

        var empty = all.Single(s => s.Check.Id == closed.Id);
        Assert.Equal(0, empty.Progress.Total);
        Assert.Null(empty.Progress.Percent);
    }

    [Fact]
    public async Task GetItems_JoinsAppAndSquad_AndListByApp_ShowsTheAppStatusInEachCheck()
    {
        var (squadId, appIds) = await SeedAppsAsync(1);
        var appId = appIds[0];
        var first = await CreateCheckAsync();
        var second = await CreateCheckAsync();
        await AddAsync(first.Id, appId);
        await AddAsync(second.Id, appId);
        await UpdateAsync(second.Id, appId, CheckItemStatus.Concluido, "PR #412");

        var items = await _checks.GetItemsAsync(second.Id);
        var item = Assert.Single(items);
        Assert.Equal(appId, item.AppId);
        Assert.Equal($"App {appId}", item.AppName);
        Assert.Equal(squadId, item.SquadId);
        Assert.Equal($"Squad {squadId}", item.SquadName);
        Assert.Equal(CheckItemStatus.Concluido, item.Status);
        Assert.Equal("PR #412", item.Notes);

        var entries = await _checks.ListByAppAsync(appId);
        Assert.Equal(2, entries.Count);
        Assert.Equal(CheckItemStatus.Pendente, entries.Single(e => e.CheckId == first.Id).ItemStatus);
        Assert.Equal(CheckItemStatus.Concluido, entries.Single(e => e.CheckId == second.Id).ItemStatus);
    }

    [Fact]
    public async Task Candidates_ExcludeAppsAlreadyInTheCheck_AndCarryTags()
    {
        var (_, appIds) = await SeedAppsAsync(2, tags: ["solucao:carrinho"]);
        var check = await CreateCheckAsync();
        await AddAsync(check.Id, appIds[0]);

        var candidates = await _checks.ListCandidateAppsAsync(check.Id);

        Assert.DoesNotContain(candidates, c => c.AppId == appIds[0]);
        var candidate = Assert.Single(candidates, c => c.AppId == appIds[1]);
        Assert.Equal(["solucao:carrinho"], candidate.Tags);
    }

    [Fact]
    public async Task AddItem_IsIdempotent_InRowsAndInHistory()
    {
        var (_, appIds) = await SeedAppsAsync(1);
        var check = await CreateCheckAsync();

        Assert.True(await AddAsync(check.Id, appIds[0]));
        Assert.False(await AddAsync(check.Id, appIds[0]));

        Assert.Single(await _checks.GetItemsAsync(check.Id));
        var history = await _checks.GetHistoryAsync(check.Id);
        var added = Assert.Single(history);
        Assert.Equal(CheckEventType.ItemAdded, added.Type);
        Assert.Equal(appIds[0], added.AppId);
    }

    [Fact]
    public async Task History_RecordsStatusChangesRemovalAndReopen_MostRecentFirst()
    {
        var (_, appIds) = await SeedAppsAsync(1);
        var appId = appIds[0];
        var check = await CreateCheckAsync();
        await AddAsync(check.Id, appId, at: T0.AddMinutes(1));

        await _checks.UpdateItemAsync(
            Item(check.Id, appId, CheckItemStatus.Pendente, "só observação", T0.AddMinutes(2)), statusChanged: null);
        await _checks.UpdateItemAsync(
            Item(check.Id, appId, CheckItemStatus.EmAndamento, null, T0.AddMinutes(3)),
            Event(check.Id, CheckEventType.ItemStatusChanged, appId, CheckItemStatus.Pendente, CheckItemStatus.EmAndamento, T0.AddMinutes(3)));
        await _checks.RemoveItemAsync(check.Id, appId,
            Event(check.Id, CheckEventType.ItemRemoved, appId, CheckItemStatus.EmAndamento, null, T0.AddMinutes(4)));

        check.Status = CheckStatus.Closed;
        check.ClosedAt = T0.AddMinutes(5);
        await _checks.SetStatusAsync(check, Event(check.Id, CheckEventType.Closed, at: T0.AddMinutes(5)));
        check.Status = CheckStatus.Open;
        check.ClosedAt = null;
        await _checks.SetStatusAsync(check, Event(check.Id, CheckEventType.Reopened, at: T0.AddMinutes(6)));

        var history = await _checks.GetHistoryAsync(check.Id);

        Assert.Equal(
            [CheckEventType.Reopened, CheckEventType.Closed, CheckEventType.ItemRemoved, CheckEventType.ItemStatusChanged, CheckEventType.ItemAdded],
            history.Select(e => e.Type));
        var removed = history.Single(e => e.Type == CheckEventType.ItemRemoved);
        Assert.Equal(CheckItemStatus.EmAndamento, removed.FromStatus);
        Assert.Empty(await _checks.GetItemsAsync(check.Id));

        var stored = await _checks.GetAsync(check.Id);
        Assert.Equal(CheckStatus.Open, stored!.Status);
        Assert.Null(stored.ClosedAt);
    }

    [Fact]
    public async Task ForeignKey_RefusesToDeleteAnAppThatIsInACheck()
    {
        var (_, appIds) = await SeedAppsAsync(1);
        var check = await CreateCheckAsync();
        await AddAsync(check.Id, appIds[0]);

        Assert.True(await _checks.IsAppInAnyCheckAsync(appIds[0]));
        await Assert.ThrowsAsync<SqliteException>(() => _apps.DeleteAllAsync(appIds[0]));
        Assert.NotNull(await _apps.GetAsync(appIds[0]));
    }

    [Fact]
    public async Task DeleteCheck_RemovesItsHistory_AndAppIsFreeAgain()
    {
        var (_, appIds) = await SeedAppsAsync(1);
        var check = await CreateCheckAsync();
        await AddAsync(check.Id, appIds[0]);
        await _checks.RemoveItemAsync(check.Id, appIds[0],
            Event(check.Id, CheckEventType.ItemRemoved, appIds[0], CheckItemStatus.Pendente));

        await _checks.DeleteAsync(check.Id);

        Assert.Null(await _checks.GetAsync(check.Id));
        Assert.Empty(await _checks.GetHistoryAsync(check.Id));
        Assert.False(await _checks.IsAppInAnyCheckAsync(appIds[0]));
    }

    private async Task<(string SquadId, List<string> AppIds)> SeedAppsAsync(int count, IReadOnlyCollection<string>? tags = null)
    {
        var squadId = $"sq-{Guid.NewGuid():N}";
        await _squads.SaveAsync(new Squad { Id = squadId, Name = $"Squad {squadId}" });

        var appIds = new List<string>();
        for (var i = 0; i < count; i++)
        {
            var appId = $"app-{Guid.NewGuid():N}";
            await _apps.SaveAsync(new App { Id = appId, SquadId = squadId, Name = $"App {appId}", Tags = tags ?? [] });
            appIds.Add(appId);
        }

        return (squadId, appIds);
    }

    private async Task<Check> CreateCheckAsync()
    {
        var check = NewCheck();
        Assert.True(await _checks.TryCreateAsync(check));
        return check;
    }

    private Task<bool> AddAsync(string checkId, string appId, DateTimeOffset? at = null) =>
        _checks.AddItemAsync(
            Item(checkId, appId, CheckItemStatus.Pendente, null, at ?? T0),
            Event(checkId, CheckEventType.ItemAdded, appId, to: CheckItemStatus.Pendente, at: at));

    private Task UpdateAsync(string checkId, string appId, CheckItemStatus status, string? notes = null) =>
        _checks.UpdateItemAsync(
            Item(checkId, appId, status, notes, T0),
            Event(checkId, CheckEventType.ItemStatusChanged, appId, CheckItemStatus.Pendente, status));

    private static Check NewCheck(string? id = null) => new()
    {
        Id = id ?? $"chk-{Guid.NewGuid():N}",
        Title = "Atualizar .NET 10",
        CreatedAt = T0
    };

    private static CheckItem Item(string checkId, string appId, CheckItemStatus status, string? notes, DateTimeOffset at) => new()
    {
        CheckId = checkId,
        AppId = appId,
        Status = status,
        Notes = notes,
        UpdatedAt = at
    };

    private static CheckEvent Event(
        string checkId, CheckEventType type, string? appId = null,
        CheckItemStatus? from = null, CheckItemStatus? to = null, DateTimeOffset? at = null) => new()
    {
        Id = Guid.NewGuid().ToString("n"),
        CheckId = checkId,
        AppId = appId,
        Type = type,
        FromStatus = from,
        ToStatus = to,
        OccurredAt = at ?? T0
    };
}
