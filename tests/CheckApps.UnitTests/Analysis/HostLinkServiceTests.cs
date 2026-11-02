using CheckApps.Application;
using CheckApps.Application.Analysis;
using CheckApps.Domain;
using CheckApps.Domain.Analysis;
using CheckApps.UnitTests.TestDoubles;

namespace CheckApps.UnitTests.Analysis;

/// <summary>Spec 017: as regras de gravar, trocar e remover vínculos host→app (a listagem com SQLite fica nos testes de integração).</summary>
public class HostLinkServiceTests
{
    private readonly FakeAppRepository _apps = new();
    private readonly FakeHostLinkRepository _links = new();

    public HostLinkServiceTests()
    {
        _apps.Add(new App { Id = "pag", SquadId = "sq-1", Name = "Pagamentos" });
        _apps.Add(new App { Id = "fat", SquadId = "sq-1", Name = "Faturas" });
    }

    private HostLinkService Sut() => new(_apps, _links, new FakeAnalysisRepository(), new AnalysisOptions(), TimeProvider.System);

    [Fact]
    public async Task Set_Confirmed_StoresTheNormalizedKey_AsHuman_WithTheAppName()
    {
        var result = await Sut().SetAsync("  Empresa.{ENV}.exemplo.com ", "pag", HostLinkStates.Confirmed, "  é o de pagamentos  ");

        Assert.Equal("empresa.{env}.exemplo.com", result.HostKey);
        Assert.Equal("Pagamentos", result.AppName);
        Assert.Equal(HostLinkStates.Confirmed, result.State);
        Assert.Equal("é o de pagamentos", result.Note);
        var stored = Assert.Single(_links.Links);
        Assert.Equal("empresa.{env}.exemplo.com", stored.HostKey);
        Assert.Equal(HostLinkOrigins.Human, stored.Origin);
        Assert.Equal(result.LinkId, stored.Id);
    }

    [Theory]
    [InlineData("https://a.exemplo.com")]
    [InlineData("")]
    [InlineData("a b")]
    public async Task Set_InvalidHostKey_IsRefused_AndNothingIsStored(string host)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().SetAsync(host, "pag", HostLinkStates.Confirmed, null));

        Assert.Empty(_links.Links);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sugerido")]     // reservado: só pessoa decide, e pessoa confirma ou rejeita
    [InlineData("talvez")]
    public async Task Set_StateOtherThanConfirmedOrRejected_IsRefused(string? state)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().SetAsync("a.exemplo.com", "pag", state, null));

        Assert.Empty(_links.Links);
    }

    [Fact]
    public async Task Set_NoteLongerThanTheLimit_IsRefused() =>
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Sut().SetAsync("a.exemplo.com", "pag", HostLinkStates.Confirmed, new string('x', HostLinkService.MaxNoteLength + 1)));

    [Theory]
    [InlineData("app-inexistente")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Set_UnknownApp_IsNotFound(string? appId) =>
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().SetAsync("a.exemplo.com", appId, HostLinkStates.Confirmed, null));

    [Fact]
    public async Task Set_SecondConfirmedAppForTheSameHost_IsAConflict_NamingTheCurrentOne()
    {
        await Sut().SetAsync("a.exemplo.com", "pag", HostLinkStates.Confirmed, null);

        var ex = await Assert.ThrowsAsync<DomainConflictException>(() => Sut().SetAsync("A.exemplo.com", "fat", HostLinkStates.Confirmed, null));

        Assert.Contains("Pagamentos", ex.Message);
        Assert.Equal("pag", Assert.Single(_links.Links).AppId);
    }

    [Fact]
    public async Task Set_ManyRejections_AreAllowed_EvenNextToAConfirmation()
    {
        await Sut().SetAsync("a.exemplo.com", "pag", HostLinkStates.Rejected, null);
        await Sut().SetAsync("a.exemplo.com", "fat", HostLinkStates.Rejected, null);

        Assert.Equal(2, _links.Links.Count);

        await Sut().SetAsync("a.exemplo.com", "pag", HostLinkStates.Confirmed, null);   // rejeito → confirmado: o mesmo par

        Assert.Equal(2, _links.Links.Count);
        Assert.Equal(HostLinkStates.Confirmed, _links.Links.Single(l => l.AppId == "pag").State);
        Assert.Equal(HostLinkStates.Rejected, _links.Links.Single(l => l.AppId == "fat").State);
    }

    [Fact]
    public async Task Set_SamePairAgain_ChangesTheState_KeepingTheId_AndClearsTheNote()
    {
        var first = await Sut().SetAsync("a.exemplo.com", "pag", HostLinkStates.Confirmed, "nota");
        var second = await Sut().SetAsync("a.exemplo.com", "pag", HostLinkStates.Rejected, null);

        Assert.Equal(first.LinkId, second.LinkId);
        var stored = Assert.Single(_links.Links);
        Assert.Equal(HostLinkStates.Rejected, stored.State);
        Assert.Null(stored.Note);
    }

    [Fact]
    public async Task Set_AfterTheConfirmedWasRemoved_AnotherAppCanBeConfirmed()
    {
        await Sut().SetAsync("a.exemplo.com", "pag", HostLinkStates.Confirmed, null);
        await Sut().RemoveAsync("a.exemplo.com", "pag");

        await Sut().SetAsync("a.exemplo.com", "fat", HostLinkStates.Confirmed, null);

        Assert.Equal("fat", Assert.Single(_links.Links).AppId);
    }

    [Fact]
    public async Task Remove_DeletesThePair_AndAMissingOneIsNotFound()
    {
        await Sut().SetAsync("a.exemplo.com", "pag", HostLinkStates.Confirmed, null);

        await Sut().RemoveAsync("A.exemplo.com", "pag");

        Assert.Empty(_links.Links);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().RemoveAsync("a.exemplo.com", "pag"));
    }

    [Fact]
    public async Task Remove_InvalidHostKey_IsRefused() =>
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().RemoveAsync("https://a.exemplo.com", "pag"));
}
