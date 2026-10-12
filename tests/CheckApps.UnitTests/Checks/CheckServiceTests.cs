using CheckApps.Application;
using CheckApps.Application.Checks;
using CheckApps.Domain;
using CheckApps.Domain.Checks;
using CheckApps.UnitTests.TestDoubles;

namespace CheckApps.UnitTests.Checks;

public class CheckServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeCheckRepository _checks = new();
    private readonly FakeAppRepository _apps = new();
    private readonly FixedTimeProvider _clock = new(T0);
    private readonly CheckService _sut;

    public CheckServiceTests()
    {
        _sut = new CheckService(_checks, _apps, _clock);
        foreach (var appId in new[] { "checkout-api", "billing-service", "auth-gateway" })
        {
            _apps.Add(new App { Id = appId, SquadId = "sq-1", Name = appId });
        }
    }

    private static CheckFields Fields(string title = "Atualizar .NET 10", DateOnly? start = null, DateOnly? end = null) =>
        new(title, null, "segurança", "Maria", start, end);

    [Fact]
    public async Task CreateAsync_GeneratesSlugId_OpenWithCreationTime_AndSuffixesDuplicates()
    {
        var first = await _sut.CreateAsync(Fields());
        var second = await _sut.CreateAsync(Fields());

        Assert.Equal("atualizar-net-10", first.Id);
        Assert.Equal("atualizar-net-10-2", second.Id);
        Assert.Equal(CheckStatus.Open, first.Status);
        Assert.Equal(T0, first.CreatedAt);
        Assert.Equal("Maria", first.Author);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_RequiresTitle(string title)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateAsync(Fields(title)));
    }

    [Fact]
    public async Task CreateAsync_RejectsEndBeforeStart()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.CreateAsync(Fields(start: new DateOnly(2026, 10, 10), end: new DateOnly(2026, 10, 1))));
    }

    [Fact]
    public async Task AddAppsAsync_IsIdempotent_AndRecordsOneEventPerAppActuallyAdded()
    {
        var check = await _sut.CreateAsync(Fields());

        var firstRound = await _sut.AddAppsAsync(check.Id, ["checkout-api", "billing-service", "checkout-api"]);
        var secondRound = await _sut.AddAppsAsync(check.Id, ["checkout-api", "auth-gateway"]);

        Assert.Equal(2, firstRound);
        Assert.Equal(1, secondRound);

        var details = await _sut.GetAsync(check.Id);
        Assert.Equal(3, details!.Items.Count);
        Assert.All(details.Items, i => Assert.Equal(CheckItemStatus.Pendente, i.Status));
        Assert.Equal(3, _checks.Events.Count(e => e.Type == CheckEventType.ItemAdded));
    }

    [Fact]
    public async Task AddAppsAsync_RejectsUnknownApp_WithoutAddingAnyOfTheBatch()
    {
        var check = await _sut.CreateAsync(Fields());

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.AddAppsAsync(check.Id, ["checkout-api", "nao-existe"]));

        Assert.Empty((await _sut.GetAsync(check.Id))!.Items);
    }

    [Fact]
    public async Task AddAppsAsync_ThrowsKeyNotFound_ForUnknownCheck()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.AddAppsAsync("nao-existe", ["checkout-api"]));
    }

    [Fact]
    public async Task UpdateItemAsync_RecordsEventOnlyWhenStatusChanges_AndProgressFollows()
    {
        var check = await _sut.CreateAsync(Fields());
        await _sut.AddAppsAsync(check.Id, ["checkout-api", "billing-service"]);

        await _sut.UpdateItemAsync(check.Id, "checkout-api", CheckItemStatus.Pendente, "só uma observação");
        await _sut.UpdateItemAsync(check.Id, "checkout-api", CheckItemStatus.Conforme, "já estava ok");

        var statusEvents = _checks.Events.Where(e => e.Type == CheckEventType.ItemStatusChanged).ToList();
        var changed = Assert.Single(statusEvents);
        Assert.Equal(CheckItemStatus.Pendente, changed.FromStatus);
        Assert.Equal(CheckItemStatus.Conforme, changed.ToStatus);

        var details = await _sut.GetAsync(check.Id);
        Assert.Equal(50.0, details!.Progress.Percent);
        Assert.Equal("já estava ok", details.Items.Single(i => i.AppId == "checkout-api").Notes);
    }

    [Fact]
    public async Task UpdateItemAsync_ThrowsKeyNotFound_WhenAppIsNotInTheCheck()
    {
        var check = await _sut.CreateAsync(Fields());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.UpdateItemAsync(check.Id, "checkout-api", CheckItemStatus.Concluido, null));
    }

    [Fact]
    public async Task RemoveAppAsync_RecordsTheLastStatus()
    {
        var check = await _sut.CreateAsync(Fields());
        await _sut.AddAppsAsync(check.Id, ["checkout-api"]);
        await _sut.UpdateItemAsync(check.Id, "checkout-api", CheckItemStatus.EmAndamento, null);

        await _sut.RemoveAppAsync(check.Id, "checkout-api");

        var removed = Assert.Single(_checks.Events, e => e.Type == CheckEventType.ItemRemoved);
        Assert.Equal(CheckItemStatus.EmAndamento, removed.FromStatus);
        Assert.Empty((await _sut.GetAsync(check.Id))!.Items);
    }

    [Fact]
    public async Task CloseAndReopen_AllowedBelow100Percent_RecordedInHistory_AndConflictWhenAlreadyInState()
    {
        var check = await _sut.CreateAsync(Fields());
        await _sut.AddAppsAsync(check.Id, ["checkout-api"]);

        _clock.Advance(TimeSpan.FromDays(1));
        var closed = await _sut.CloseAsync(check.Id);
        Assert.Equal(CheckStatus.Closed, closed.Status);
        Assert.Equal(T0.AddDays(1), closed.ClosedAt);
        await Assert.ThrowsAsync<DomainConflictException>(() => _sut.CloseAsync(check.Id));

        _clock.Advance(TimeSpan.FromDays(1));
        var reopened = await _sut.ReopenAsync(check.Id);
        Assert.Equal(CheckStatus.Open, reopened.Status);
        Assert.Null(reopened.ClosedAt);
        await Assert.ThrowsAsync<DomainConflictException>(() => _sut.ReopenAsync(check.Id));

        var history = await _sut.GetHistoryAsync(check.Id);
        Assert.Equal([CheckEventType.Reopened, CheckEventType.Closed, CheckEventType.ItemAdded], history.Select(e => e.Type));
    }

    [Fact]
    public async Task DeleteAsync_OnlyWithoutItems()
    {
        var withItems = await _sut.CreateAsync(Fields("Com apps"));
        var empty = await _sut.CreateAsync(Fields("Criada por engano"));
        await _sut.AddAppsAsync(withItems.Id, ["checkout-api"]);

        await Assert.ThrowsAsync<DomainConflictException>(() => _sut.DeleteAsync(withItems.Id));
        await _sut.DeleteAsync(empty.Id);

        Assert.NotNull(await _sut.GetAsync(withItems.Id));
        Assert.Null(await _sut.GetAsync(empty.Id));
    }

    [Fact]
    public async Task UpdateAsync_ChangesMetadataButKeepsIdAndStatus()
    {
        var check = await _sut.CreateAsync(Fields());

        var updated = await _sut.UpdateAsync(check.Id, Fields("Atualizar para .NET 10 LTS"));

        Assert.Equal(check.Id, updated.Id);
        Assert.Equal("Atualizar para .NET 10 LTS", updated.Title);
        Assert.Equal(CheckStatus.Open, updated.Status);
    }
}
