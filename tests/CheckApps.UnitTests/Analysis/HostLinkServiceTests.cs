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
    private readonly FakeHostExternalRepository _externals = new();

    public HostLinkServiceTests()
    {
        _apps.Add(new App { Id = "pag", SquadId = "sq-1", Name = "Pagamentos" });
        _apps.Add(new App { Id = "fat", SquadId = "sq-1", Name = "Faturas" });
    }

    private HostLinkService Sut() => new(_apps, _links, _externals, new FakeAnalysisRepository(), new AnalysisOptions(), TimeProvider.System);

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


    // ---- Host externo (spec 019) ---------------------------------------------------------------------------------------

    [Fact]
    public async Task SetExternal_StoresTheNormalizedKey_AsHuman_AndChangesTheSameRow()
    {
        var first = await Sut().SetExternalAsync("  API.Outra.com ", "  API da squad X ", "nota");
        var second = await Sut().SetExternalAsync("api.outra.com", null, null);

        Assert.Equal("api.outra.com", first.HostKey);
        Assert.Equal("API da squad X", first.Name);
        Assert.Equal(first.ExternalId, second.ExternalId);
        var stored = Assert.Single(_externals.Externals);
        Assert.Null(stored.Name);
        Assert.Equal(HostLinkOrigins.Human, stored.Origin);
    }

    [Fact]
    public async Task SetExternal_InvalidKeyOrLongText_IsRefused()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().SetExternalAsync("https://a.com", null, null));
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().SetExternalAsync("a.com", new string('x', HostLinkService.MaxNameLength + 1), null));
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().SetExternalAsync("a.com", null, new string('x', HostLinkService.MaxNoteLength + 1)));

        Assert.Empty(_externals.Externals);
    }

    [Fact]
    public async Task SetExternal_OnAHostConfirmedForAnApp_IsAConflict_NamingTheApp()
    {
        await Sut().SetAsync("a.exemplo.com", "pag", HostLinkStates.Confirmed, null);

        var ex = await Assert.ThrowsAsync<DomainConflictException>(() => Sut().SetExternalAsync("a.exemplo.com", null, null));

        Assert.Contains("Pagamentos", ex.Message);
        Assert.Empty(_externals.Externals);
    }

    [Fact]
    public async Task SetExternal_ConvivesWithARejection()
    {
        await Sut().SetAsync("a.exemplo.com", "pag", HostLinkStates.Rejected, null);

        await Sut().SetExternalAsync("a.exemplo.com", "Terceiro", null);

        Assert.Single(_externals.Externals);
        Assert.Single(_links.Links);
    }

    [Fact]
    public async Task Set_Confirmed_OnAnExternalHost_IsAConflict_UntilTheMarkIsRemoved()
    {
        await Sut().SetExternalAsync("a.exemplo.com", "Terceiro", null);

        var ex = await Assert.ThrowsAsync<DomainConflictException>(() => Sut().SetAsync("a.exemplo.com", "pag", HostLinkStates.Confirmed, null));
        Assert.Contains("Terceiro", ex.Message);

        await Sut().RemoveExternalAsync("a.exemplo.com");
        await Sut().SetAsync("a.exemplo.com", "pag", HostLinkStates.Confirmed, null);
        Assert.Single(_links.Links);
    }

    [Fact]
    public async Task RemoveExternal_Missing_IsNotFound_AndInvalidKeyIsRefused()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().RemoveExternalAsync("a.exemplo.com"));
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().RemoveExternalAsync("a b"));
    }

    // ---- Lote (spec 019) -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task Batch_Confirmed_AppliesToEveryHost_AndDropsDuplicates()
    {
        var results = await Sut().ApplyBatchAsync(
            ["a.dev.exemplo.com", "A.hom.exemplo.com", "a.hom.exemplo.com"], HostLinkStates.Confirmed, "pag", null, "lote");

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Ok));
        Assert.Equal(2, _links.Links.Count);
        Assert.All(_links.Links, l => Assert.Equal("pag", l.AppId));
    }

    [Fact]
    public async Task Batch_OneHostFailing_DoesNotUndoTheOthers_AndReportsItsError()
    {
        await Sut().SetAsync("b.exemplo.com", "fat", HostLinkStates.Confirmed, null);   // já é de outro app

        var results = await Sut().ApplyBatchAsync(
            ["a.exemplo.com", "b.exemplo.com", "https://ruim.com"], HostLinkStates.Confirmed, "pag", null, null);

        Assert.True(results.Single(r => r.HostKey == "a.exemplo.com").Ok);
        Assert.Contains("Faturas", results.Single(r => r.HostKey == "b.exemplo.com").Error);
        Assert.False(results.Single(r => r.HostKey == "https://ruim.com").Ok);
        Assert.Contains(_links.Links, l => l.HostKey == "a.exemplo.com" && l.AppId == "pag");
    }

    [Fact]
    public async Task Batch_External_NeedsNoApp_AndStoresTheName()
    {
        var results = await Sut().ApplyBatchAsync(["x.outra.com", "y.outra.com"], HostLinkService.ExternalDecision, null, "Squad X", null);

        Assert.All(results, r => Assert.True(r.Ok));
        Assert.Equal(2, _externals.Externals.Count);
        Assert.All(_externals.Externals, e => Assert.Equal("Squad X", e.Name));
    }

    [Theory]
    [InlineData("confirmado", null)]          // sem app
    [InlineData("rejeitado", "")]
    [InlineData("sugerido", "pag")]           // reservado
    [InlineData(null, "pag")]
    [InlineData("talvez", "pag")]
    public async Task Batch_InvalidDecision_RefusesTheWholeRequest(string? state, string? appId)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().ApplyBatchAsync(["a.exemplo.com"], state, appId, null, null));

        Assert.Empty(_links.Links);
    }

    [Fact]
    public async Task Batch_EmptyOrTooBig_IsRefused()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().ApplyBatchAsync([], HostLinkStates.Confirmed, "pag", null, null));
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().ApplyBatchAsync(null, HostLinkStates.Confirmed, "pag", null, null));

        var many = Enumerable.Range(0, HostLinkService.MaxBatchSize + 1).Select(i => $"h{i}.exemplo.com").ToList();
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().ApplyBatchAsync(many, HostLinkStates.Confirmed, "pag", null, null));
    }
}
