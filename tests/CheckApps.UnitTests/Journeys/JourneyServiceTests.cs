using CheckApps.Application;
using CheckApps.Application.Analysis;
using CheckApps.Application.Apps;
using CheckApps.Application.Journeys;
using CheckApps.Application.Squads;
using CheckApps.Domain;
using CheckApps.UnitTests.TestDoubles;
using static CheckApps.Application.Analysis.ContractChangeKinds;
using static CheckApps.Application.Analysis.ContractChangeSeverities;
using static CheckApps.UnitTests.TestDoubles.FakeContractReader;

namespace CheckApps.UnitTests.Journeys;

/// <summary>As regras da árvore e dos passos (spec 022), com repositórios e contratos em memória.</summary>
public class JourneyServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeJourneyRepository _journeys = new();
    private readonly FakeSquadRepository _squads = new();
    private readonly FakeAppRepository _apps = new();
    private readonly FakeContractReader _contracts = new();
    private readonly FixedTimeProvider _clock = new(T0);

    public JourneyServiceTests()
    {
        _squads.SaveAsync(new Squad { Id = "sq-1", Name = "Squad Fundos" }).Wait();
        _apps.Add(new App { Id = "app-bff", SquadId = "sq-1", Name = "BFF Fundos" });
        _apps.Add(new App { Id = "app-api", SquadId = "sq-1", Name = "API Fundos" });
    }

    private JourneyService Sut() => new(_journeys, _squads, _apps, _contracts, _clock);

    private static JourneyInput Input(string name, string? parent = null, string? level = null, string? squad = null, params string[] tags) =>
        new(name, null, level, squad, parent, tags);

    private static StepInput Step(string appId, string method, string path, string? label = null) => new(appId, method, path, label, null);

    private async Task<Journey> CreateAsync(string name, string? parent = null, string? level = null) =>
        await Sut().CreateAsync(Input(name, parent, level));

    private async Task<Journey> LeafWithStepsAsync()
    {
        var leaf = await CreateAsync("Compra de Fundos");
        await Sut().ReplaceStepsAsync(leaf.Id, [Step("app-bff", "GET", "/fundos")]);
        return leaf;
    }

    // ---- US1: a árvore ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Create_ARoot_GetsASlugId_TheManualSource_AndTheTimestamps()
    {
        var journey = await Sut().CreateAsync(new JourneyInput("Plataforma de Investimentos", "  descrição  ", " plataforma ", "sq-1", null, [" a:b ", "a:b", "", "x"]));

        Assert.Equal("plataforma-de-investimentos", journey.Id);
        Assert.Equal("descrição", journey.Description);
        Assert.Equal("plataforma", journey.Level);
        Assert.Equal("sq-1", journey.SquadId);
        Assert.Null(journey.ParentId);
        Assert.Equal(["a:b", "x"], journey.Tags);
        Assert.Equal(JourneySources.Manual, journey.Source);
        Assert.Equal(T0, journey.CreatedAt);
        Assert.Equal(T0, journey.UpdatedAt);
    }

    [Fact]
    public async Task Create_ThreeLevels_AndASlugCollisionGetsASuffix()
    {
        var platform = await CreateAsync("Plataforma", level: "plataforma");
        var system = await CreateAsync("Sistema", platform.Id, "sistema");
        var journey = await CreateAsync("Compra de Fundos", system.Id, "jornada");
        var sameSlugElsewhere = await CreateAsync("Compra de Fundos", platform.Id);

        Assert.Equal(system.Id, journey.ParentId);
        Assert.Equal("compra-de-fundos", journey.Id);
        Assert.Equal("compra-de-fundos-2", sameSlugElsewhere.Id);
    }

    [Fact]
    public async Task Create_BlankLevelBecomesNull_AndALongOneIsRefused()
    {
        Assert.Null((await Sut().CreateAsync(Input("A", level: "   "))).Level);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Sut().CreateAsync(Input("B", level: new string('x', 41))));
        Assert.Contains("40", ex.Message);
        Assert.Null(ex.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Create_WithoutAName_IsRefused(string? name) =>
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().CreateAsync(Input(name!)));

    [Fact]
    public async Task Create_WithAnUnknownSquadOrParent_IsABadRequest_NotANotFound()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().CreateAsync(Input("A", squad: "nao-existe")));
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().CreateAsync(Input("B", parent: "nao-existe")));
    }

    [Fact]
    public async Task Create_TooManyOrTooLongTags_AreRefused()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().CreateAsync(Input("A", tags: Enumerable.Range(0, 21).Select(i => $"t{i}").ToArray())));
        await Assert.ThrowsAsync<ArgumentException>(() => Sut().CreateAsync(Input("B", tags: new string('t', 61))));
    }

    [Fact]
    public async Task Create_TheSameNameAmongSiblings_ConflictsWithoutCaseDifference_ButNotUnderAnotherParent()
    {
        var one = await CreateAsync("Sistema Um");
        var two = await CreateAsync("Sistema Dois");
        await CreateAsync("Compra", one.Id);

        await Assert.ThrowsAsync<DomainConflictException>(() => Sut().CreateAsync(Input("COMPRA", one.Id)));
        await Assert.ThrowsAsync<DomainConflictException>(() => Sut().CreateAsync(Input("sistema um")));
        Assert.NotNull(await CreateAsync("Compra", two.Id));
    }

    [Fact]
    public async Task Create_AChildOfAJourneyThatHasSteps_Conflicts()
    {
        var leaf = await LeafWithStepsAsync();

        var ex = await Assert.ThrowsAsync<DomainConflictException>(() => Sut().CreateAsync(Input("Filha", leaf.Id)));
        Assert.Contains("passos", ex.Message);
    }

    [Fact]
    public async Task Create_BeyondTheMaximumDepth_Conflicts()
    {
        string? parent = null;
        for (var level = 1; level <= JourneyLimits.MaxDepth; level++)
        {
            parent = (await CreateAsync($"Nível {level}", parent)).Id;
        }

        await Assert.ThrowsAsync<DomainConflictException>(() => Sut().CreateAsync(Input("Fundo demais", parent)));
    }

    [Fact]
    public async Task Update_ChangesTheFields_ButKeepsTheIdSourceAndCreationDate()
    {
        var journey = await CreateAsync("Compra");
        _clock.Advance(TimeSpan.FromDays(1));

        var updated = await Sut().UpdateAsync(journey.Id, new JourneyInput("Compra renomeada", "nova", "jornada", "sq-1", null, ["tipo:fundos"]));

        Assert.Equal(journey.Id, updated.Id);
        Assert.Equal("Compra renomeada", updated.Name);
        Assert.Equal("sq-1", updated.SquadId);
        Assert.Equal(["tipo:fundos"], updated.Tags);
        Assert.Equal(T0, updated.CreatedAt);
        Assert.Equal(T0.AddDays(1), updated.UpdatedAt);
        Assert.Equal(updated, await _journeys.GetAsync(journey.Id));
    }

    [Fact]
    public async Task Update_AJourneyThatDoesNotExist_IsNotFound() =>
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().UpdateAsync("nao-existe", Input("A")));

    [Fact]
    public async Task Update_ToItselfOrToADescendant_ConflictsBecauseItWouldBeACycle()
    {
        var platform = await CreateAsync("Plataforma");
        var system = await CreateAsync("Sistema", platform.Id);
        var journey = await CreateAsync("Compra", system.Id);

        var self = await Assert.ThrowsAsync<DomainConflictException>(() => Sut().UpdateAsync(platform.Id, Input("Plataforma", platform.Id)));
        await Assert.ThrowsAsync<DomainConflictException>(() => Sut().UpdateAsync(platform.Id, Input("Plataforma", journey.Id)));
        Assert.Contains("ciclo", self.Message);
    }

    [Fact]
    public async Task Update_MovingASubtree_RespectsTheDepthOfTheWholeSubtree()
    {
        string? parent = null;
        for (var level = 1; level <= JourneyLimits.MaxDepth - 1; level++)
        {
            parent = (await CreateAsync($"Nível {level}", parent)).Id;
        }

        var root = await CreateAsync("Outra raiz");
        await CreateAsync("Filha", root.Id);

        // A "Outra raiz" tem 2 níveis; sob o nível 7, ficaria com 9.
        await Assert.ThrowsAsync<DomainConflictException>(() => Sut().UpdateAsync(root.Id, Input("Outra raiz", parent)));
    }

    [Fact]
    public async Task Update_MovingUnderAJourneyThatHasSteps_Conflicts()
    {
        var leaf = await LeafWithStepsAsync();
        var other = await CreateAsync("Outra");

        await Assert.ThrowsAsync<DomainConflictException>(() => Sut().UpdateAsync(other.Id, Input("Outra", leaf.Id)));
    }

    [Fact]
    public async Task Update_RenamingToASiblingsName_Conflicts_ButKeepingItsOwnNameIsFine()
    {
        await CreateAsync("Compra");
        var sale = await CreateAsync("Venda");

        await Assert.ThrowsAsync<DomainConflictException>(() => Sut().UpdateAsync(sale.Id, Input("compra")));
        Assert.Equal("Venda", (await Sut().UpdateAsync(sale.Id, Input("Venda", level: "jornada"))).Name);
    }

    [Fact]
    public async Task Delete_AJourneyWithChildren_Conflicts_ButALeafGoesWithItsSteps()
    {
        var system = await CreateAsync("Sistema");
        var leaf = await CreateAsync("Compra", system.Id);
        await Sut().ReplaceStepsAsync(leaf.Id, [Step("app-bff", "GET", "/fundos")]);

        var ex = await Assert.ThrowsAsync<DomainConflictException>(() => Sut().DeleteAsync(system.Id));
        Assert.Contains("1 jornada", ex.Message);

        await Sut().DeleteAsync(leaf.Id);
        Assert.Null(await _journeys.GetAsync(leaf.Id));
        Assert.Empty(await _journeys.GetStepsAsync(leaf.Id));
        await Sut().DeleteAsync(system.Id);
    }

    [Fact]
    public async Task Delete_AJourneyThatDoesNotExist_IsNotFound() =>
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().DeleteAsync("nao-existe"));

    // ---- US2: os passos -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task ReplaceSteps_StoresThemInOrder_WithTheMethodUppercasedAndTheRouteKey()
    {
        var leaf = await CreateAsync("Compra de Fundos");

        await Sut().ReplaceStepsAsync(leaf.Id,
        [
            Step("app-bff", "get", " /fundos/{codigoFundo} ", "Consultar fundos"),
            Step("app-bff", "POST", "/fundos/simulacoes", "Simular"),
            Step("app-api", "post", "/ordens")
        ]);

        var steps = await _journeys.GetStepsAsync(leaf.Id);
        Assert.Equal([0, 1, 2], steps.Select(s => s.Position));
        Assert.Equal(["GET", "POST", "POST"], steps.Select(s => s.Method));
        Assert.Equal("/fundos/{codigoFundo}", steps[0].Path);
        Assert.Equal("/fundos/{}", steps[0].PathKey);
        Assert.Equal("Consultar fundos", steps[0].Label);
        Assert.All(steps, s => Assert.Equal(leaf.Id, s.JourneyId));
    }

    [Fact]
    public async Task ReplaceSteps_ReplacesTheWholeList_AndAnEmptyListClearsIt()
    {
        var leaf = await LeafWithStepsAsync();

        await Sut().ReplaceStepsAsync(leaf.Id, [Step("app-api", "GET", "/novo")]);
        Assert.Equal(["/novo"], (await _journeys.GetStepsAsync(leaf.Id)).Select(s => s.Path));

        await Sut().ReplaceStepsAsync(leaf.Id, []);
        Assert.Empty(await _journeys.GetStepsAsync(leaf.Id));
    }

    [Theory]
    [InlineData("app-inexistente", "GET", "/x")]
    [InlineData("", "GET", "/x")]
    [InlineData("app-bff", "FETCH", "/x")]
    [InlineData("app-bff", "", "/x")]
    [InlineData("app-bff", "GET", "x")]
    [InlineData("app-bff", "GET", "")]
    public async Task ReplaceSteps_InvalidInput_IsABadRequest_AndNothingIsStored(string appId, string method, string path)
    {
        var leaf = await LeafWithStepsAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => Sut().ReplaceStepsAsync(leaf.Id, [Step("app-api", "GET", "/ok"), Step(appId, method, path)]));

        Assert.Equal(["/fundos"], (await _journeys.GetStepsAsync(leaf.Id)).Select(s => s.Path));
    }

    [Fact]
    public async Task ReplaceSteps_MoreThanTheLimit_IsABadRequest()
    {
        var leaf = await CreateAsync("Compra");
        var tooMany = Enumerable.Range(0, JourneyLimits.MaxSteps + 1).Select(i => Step("app-bff", "GET", $"/r{i}")).ToList();

        await Assert.ThrowsAsync<ArgumentException>(() => Sut().ReplaceStepsAsync(leaf.Id, tooMany));
    }

    [Fact]
    public async Task ReplaceSteps_OnAJourneyWithChildren_Conflicts_ButClearingIsAllowed()
    {
        var system = await CreateAsync("Sistema");
        await CreateAsync("Compra", system.Id);

        await Assert.ThrowsAsync<DomainConflictException>(() => Sut().ReplaceStepsAsync(system.Id, [Step("app-bff", "GET", "/x")]));
        await Sut().ReplaceStepsAsync(system.Id, []);
    }

    [Fact]
    public async Task ReplaceSteps_AJourneyThatDoesNotExist_IsNotFound() =>
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().ReplaceStepsAsync("nao-existe", []));

    [Fact]
    public async Task ReplaceSteps_TheSameEndpointTwice_IsAllowed()
    {
        var leaf = await CreateAsync("Compra");

        await Sut().ReplaceStepsAsync(leaf.Id, [Step("app-bff", "GET", "/x"), Step("app-bff", "GET", "/x")]);

        Assert.Equal(2, (await _journeys.GetStepsAsync(leaf.Id)).Count);
    }

    // ---- Detalhe: caminho, filhos, passos resolvidos e apps derivados --------------------------------------------------

    [Fact]
    public async Task Detail_ShowsThePathTheChildrenAndTheSquad()
    {
        var platform = await Sut().CreateAsync(Input("Plataforma", level: "plataforma", squad: "sq-1"));
        var system = await CreateAsync("Sistema", platform.Id);
        await CreateAsync("Venda", system.Id);
        var purchase = await CreateAsync("Compra", system.Id);
        await Sut().ReplaceStepsAsync(purchase.Id, [Step("app-bff", "GET", "/fundos")]);

        var detail = await Sut().GetDetailAsync(system.Id);

        Assert.Equal(["plataforma"], detail.Path.Select(j => j.Id));
        Assert.Equal(["Compra", "Venda"], detail.Children.Select(c => c.Journey.Name));
        Assert.Equal(1, detail.Children[0].StepCount);
        Assert.Null(detail.SquadName);
        Assert.Equal("Squad Fundos", (await Sut().GetDetailAsync(platform.Id)).SquadName);
    }

    [Fact]
    public async Task Detail_ResolvesEachStepAgainstTheCurrentContract()
    {
        _contracts.WithEndpoints("app-bff", Endpoint("GET", "/fundos", "Consulta fundos"), Endpoint("POST", "/simulacoes/{id}"));
        var leaf = await CreateAsync("Compra");
        await Sut().ReplaceStepsAsync(leaf.Id,
        [
            Step("app-bff", "GET", "/fundos"),
            Step("app-bff", "POST", "/simulacoes/{fundoId}"),
            Step("app-bff", "DELETE", "/fundos"),
            Step("app-api", "GET", "/qualquer")
        ]);

        var steps = (await Sut().GetDetailAsync(leaf.Id)).Steps;

        Assert.Equal(
            [StepResolutions.Cataloged, StepResolutions.Cataloged, StepResolutions.OutsideContract, StepResolutions.AppWithoutContract],
            steps.Select(s => s.Resolution));
        Assert.Equal("Consulta fundos", steps[0].Endpoint!.Summary);
        Assert.Equal("BFF Fundos", steps[0].AppName);
        Assert.Equal("API Fundos", steps[3].AppName);
    }

    [Fact]
    public async Task Detail_DerivesTheAppsFromTheStepsOfTheWholeSubtree_WithTheirStepCount()
    {
        var system = await CreateAsync("Sistema");
        var purchase = await CreateAsync("Compra", system.Id);
        var sale = await CreateAsync("Venda", system.Id);
        await Sut().ReplaceStepsAsync(purchase.Id, [Step("app-bff", "GET", "/a"), Step("app-bff", "GET", "/b")]);
        await Sut().ReplaceStepsAsync(sale.Id, [Step("app-bff", "GET", "/c"), Step("app-api", "POST", "/d")]);

        var apps = (await Sut().GetDetailAsync(system.Id)).Apps;

        Assert.Equal([("API Fundos", 1), ("BFF Fundos", 3)], apps.Select(a => (a.AppName, a.StepCount)));
        Assert.Equal(2, (await Sut().GetDetailAsync(purchase.Id)).Apps.Single().StepCount);
    }

    [Fact]
    public async Task Detail_AJourneyThatDoesNotExist_IsNotFound() =>
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().GetDetailAsync("nao-existe"));

    [Fact]
    public async Task List_CountsStepsAndChildren_OrderedByName()
    {
        var system = await CreateAsync("Sistema");
        var purchase = await CreateAsync("Compra", system.Id);
        await Sut().ReplaceStepsAsync(purchase.Id, [Step("app-bff", "GET", "/a")]);

        var list = await Sut().ListAsync();

        Assert.Equal(["Compra", "Sistema"], list.Select(s => s.Journey.Name));
        Assert.Equal(1, list.Single(s => s.Journey.Name == "Compra").StepCount);
        Assert.Equal(1, list.Single(s => s.Journey.Name == "Sistema").ChildCount);
    }

    // ---- US3: o impacto ----------------------------------------------------------------------------------------------

    [Fact]
    public async Task Impact_OfASystem_SumsTheStepsOfItsJourneys()
    {
        _contracts.WithChanges("app-bff", ContractChangeStates.Compared,
            Change(Breaking, BecameRequired, "POST", "/simulacoes", "contaOrigem"),
            Change(Breaking, EndpointRemoved, "GET", "/relatorios"));
        var system = await CreateAsync("Sistema");
        var purchase = await CreateAsync("Compra", system.Id);
        var sale = await CreateAsync("Venda", system.Id);
        await Sut().ReplaceStepsAsync(purchase.Id, [Step("app-bff", "POST", "/simulacoes")]);
        await Sut().ReplaceStepsAsync(sale.Id, [Step("app-bff", "GET", "/relatorios"), Step("app-bff", "GET", "/fundos")]);

        var systemImpact = await Sut().GetImpactAsync(system.Id);
        var purchaseImpact = await Sut().GetImpactAsync(purchase.Id);

        Assert.Equal(2, systemImpact.Breaking);
        Assert.Equal(2, systemImpact.BreakingSteps);
        Assert.Equal(3, systemImpact.Steps.Count);
        Assert.Contains(systemImpact.Steps, s => s.EndpointRemoved && s.JourneyName == "Venda");
        Assert.Equal(1, purchaseImpact.Breaking);
        Assert.Single(purchaseImpact.Steps);
    }

    [Fact]
    public async Task Impact_OfAnAppThatWasNeverCompared_IsUnknown()
    {
        var leaf = await CreateAsync("Compra");
        await Sut().ReplaceStepsAsync(leaf.Id, [Step("app-api", "GET", "/x")]);

        var impact = await Sut().GetImpactAsync(leaf.Id);

        Assert.Equal(1, impact.StepsWithoutComparison);
        Assert.Equal(0, impact.Breaking);
    }

    [Fact]
    public async Task Impact_AJourneyWithoutSteps_IsEmpty_AndAMissingOneIsNotFound()
    {
        var leaf = await CreateAsync("Compra");

        Assert.Empty((await Sut().GetImpactAsync(leaf.Id)).Steps);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Sut().GetImpactAsync("nao-existe"));
    }

    // ---- Guardas nos cadastros vizinhos -------------------------------------------------------------------------------

    [Fact]
    public async Task RemovingAnAppUsedByAStep_Conflicts_InsteadOfFailingOnTheForeignKey()
    {
        await LeafWithStepsAsync();
        var appService = new AppService(_apps, _squads, new FakeQualitySnapshotRepository(), new FakeCheckRepository(), _journeys);

        var ex = await Assert.ThrowsAsync<DomainConflictException>(() => appService.DeleteAsync("app-bff"));
        Assert.Contains("passos de jornadas", ex.Message);

        await appService.DeleteAsync("app-api");
    }

    [Fact]
    public async Task RemovingASquadThatOwnsAJourney_Conflicts_ButAnotherSquadIsFine()
    {
        await Sut().CreateAsync(Input("Compra", squad: "sq-1"));
        await _squads.SaveAsync(new Squad { Id = "sq-2", Name = "Outra" });
        var squadService = new SquadService(_squads, new FakeAppRepository(), new FakeQualitySnapshotRepository(), _journeys);

        var ex = await Assert.ThrowsAsync<DomainConflictException>(() => squadService.DeleteAsync("sq-1"));
        Assert.Contains("1 jornada", ex.Message);

        await squadService.DeleteAsync("sq-2");
    }
}
