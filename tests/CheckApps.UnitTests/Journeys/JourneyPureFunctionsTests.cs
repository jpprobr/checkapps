using CheckApps.Application.Analysis;
using CheckApps.Application.Journeys;
using CheckApps.Domain;
using CheckApps.Domain.Analysis;
using CheckApps.UnitTests.TestDoubles;
using Repository = CheckApps.Domain.Repository;
using static CheckApps.Application.Analysis.ContractChangeKinds;
using static CheckApps.Application.Analysis.ContractChangeSeverities;
using static CheckApps.UnitTests.TestDoubles.FakeContractReader;

namespace CheckApps.UnitTests.Journeys;

/// <summary>As funções puras das jornadas (spec 022): árvore, resolução dos passos e impacto das mudanças de contrato.</summary>
public class JourneyPureFunctionsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    private static Journey Node(string id, string? parent = null) =>
        new() { Id = id, Name = id, ParentId = parent, CreatedAt = T0, UpdatedAt = T0 };

    private static JourneyStep Step(string journeyId, string method, string path, string appId = "app-1", int position = 0) =>
        new() { Id = $"{journeyId}-{position}", JourneyId = journeyId, Position = position, AppId = appId, Method = method, Path = path, PathKey = ApiPath.KeyOf(path) };

    // ---- Árvore -------------------------------------------------------------------------------------------------------

    private static readonly IReadOnlyList<Journey> Tree = [Node("plataforma"), Node("sistema", "plataforma"), Node("compra", "sistema"), Node("venda", "sistema"), Node("outra")];

    [Fact]
    public void PathTo_ListsAncestorsFromTheRoot_AndIsEmptyForARoot()
    {
        Assert.Equal(["plataforma", "sistema"], JourneyTree.PathTo(Tree, Tree[2]).Select(j => j.Id));
        Assert.Empty(JourneyTree.PathTo(Tree, Tree[0]));
    }

    [Fact]
    public void DescendantIds_ReturnsEveryLevelButNotTheNodeItself() =>
        Assert.Equal(["compra", "sistema", "venda"], JourneyTree.DescendantIds(Tree, "plataforma").Order().ToList());

    [Theory]
    [InlineData(null, 0)]
    [InlineData("plataforma", 1)]
    [InlineData("sistema", 2)]
    [InlineData("compra", 3)]
    public void Depth_CountsLevelsFromTheRoot(string? id, int expected) => Assert.Equal(expected, JourneyTree.Depth(Tree, id));

    [Theory]
    [InlineData("plataforma", 3)]
    [InlineData("sistema", 2)]
    [InlineData("compra", 1)]
    public void Height_CountsLevelsOfTheSubtreeIncludingTheNode(string id, int expected) => Assert.Equal(expected, JourneyTree.Height(Tree, id));

    [Fact]
    public void BadStoredData_ACycle_DoesNotHang()
    {
        IReadOnlyList<Journey> cycle = [Node("a", "b"), Node("b", "a")];

        Assert.Equal(2, JourneyTree.Depth(cycle, "a"));
        Assert.Equal(2, JourneyTree.Height(cycle, "a"));
        Assert.Equal(["b"], JourneyTree.DescendantIds(cycle, "a"));
        Assert.Single(JourneyTree.PathTo(cycle, cycle[0]));
    }

    // ---- Resolução ----------------------------------------------------------------------------------------------------

    private static AppContracts Contracts(params RepositoryApiEndpoint[] endpoints) =>
        new FakeContractReader().WithEndpoints("app-1", endpoints).GetForAppAsync("app-1").Result;

    [Fact]
    public void Resolve_FindsTheEndpointByMethodAndRoute_IgnoringTheParameterName()
    {
        var contracts = Contracts(Endpoint("GET", "/fundos/{codigoFundo}", "Detalha um fundo", deprecated: true));

        var (resolution, endpoint) = JourneyResolver.Resolve(Step("j", "GET", "/fundos/{id}"), contracts);

        Assert.Equal(StepResolutions.Cataloged, resolution);
        Assert.Equal("Detalha um fundo", endpoint!.Summary);
        Assert.True(endpoint.Deprecated);
        Assert.Equal(["oauth (oauth2)"], endpoint.Security);
        Assert.Equal("app-1-contrato", endpoint.RepositoryName);
    }

    [Fact]
    public void Resolve_AnotherMethodOnTheSameRoute_IsOutsideTheContract() =>
        Assert.Equal(StepResolutions.OutsideContract, JourneyResolver.Resolve(Step("j", "POST", "/fundos"), Contracts(Endpoint("GET", "/fundos"))).Resolution);

    [Fact]
    public void Resolve_AppWithoutAnyEndpoint_IsAppWithoutContract_NotOutsideTheContract()
    {
        Assert.Equal(StepResolutions.AppWithoutContract, JourneyResolver.Resolve(Step("j", "GET", "/x"), new AppContracts("app-1", [])).Resolution);
        Assert.Equal(StepResolutions.AppWithoutContract, JourneyResolver.Resolve(Step("j", "GET", "/x"), null).Resolution);
    }

    // ---- Impacto ------------------------------------------------------------------------------------------------------

    private static IReadOnlyDictionary<string, AppChangeSet> Changes(string comparison, params ContractChange[] changes) =>
        new Dictionary<string, AppChangeSet> { ["app-1"] = new("App Um", comparison, changes, []) };

    private static JourneyImpact Compute(IReadOnlyList<JourneyStep> steps, IReadOnlyDictionary<string, AppChangeSet> byApp) =>
        JourneyImpactCalculator.Compute("j", steps.Select(s => new ImpactStepInput(s, s.JourneyId)).ToList(), byApp);

    [Fact]
    public void Impact_AMatchingChange_IsCountedBySeverity_AndOnlyOnTheStepsItHits()
    {
        var impact = Compute(
            [Step("compra", "POST", "/simulacoes", position: 0), Step("compra", "GET", "/fundos", position: 1)],
            Changes(ContractChangeStates.Compared,
                Change(Breaking, BecameRequired, "POST", "/simulacoes", "contaOrigem"),
                Change(Safe, FieldAdded, "POST", "/simulacoes", "perfil"),
                Change(Attention, EnumValueAdded, "GET", "/outra")));

        Assert.Equal(1, impact.Breaking);
        Assert.Equal(0, impact.Attention);
        Assert.Equal(1, impact.Safe);
        Assert.Equal(1, impact.BreakingSteps);
        Assert.Equal(2, impact.Steps.Single(s => s.Step.Position == 0).Changes.Count);
        Assert.Empty(impact.Steps.Single(s => s.Step.Position == 1).Changes);
    }

    [Fact]
    public void Impact_MatchesByRouteKey_SoARenamedPathParameterStillHits() =>
        Assert.Equal(1, Compute(
            [Step("j", "GET", "/fundos/{id}")],
            Changes(ContractChangeStates.Compared, Change(Breaking, TypeChanged, "GET", "/fundos/{codigoFundo}", "x"))).Breaking);

    [Fact]
    public void Impact_ARemovedEndpoint_MarksTheStep_EvenWhenOnlyAttentionBecauseOfAPartialAnalysis()
    {
        var impact = Compute(
            [Step("j", "GET", "/relatorios")],
            Changes(ContractChangeStates.Compared, Change(Attention, EndpointRemoved, "GET", "/relatorios")));

        Assert.True(impact.Steps[0].EndpointRemoved);
        Assert.Equal(0, impact.BreakingSteps);
        Assert.Equal(1, impact.Attention);
    }

    [Theory]
    [InlineData(ContractChangeStates.FirstAnalysis)]
    [InlineData(ContractChangeStates.NoContract)]
    public void Impact_AnAppWithNothingToCompare_IsUnknown_NeverNoImpact(string state)
    {
        var impact = Compute([Step("j", "GET", "/fundos")], Changes(state));

        Assert.Equal(1, impact.StepsWithoutComparison);
        Assert.Equal(state, impact.Steps[0].Comparison);
        Assert.Equal(0, impact.Breaking);
    }

    [Fact]
    public void Impact_AnAppMissingFromTheChanges_IsAlsoUnknown()
    {
        var impact = Compute([Step("j", "GET", "/fundos", appId: "app-sumiu")], Changes(ContractChangeStates.Compared));

        Assert.Equal(1, impact.StepsWithoutComparison);
        Assert.Equal(ContractChangeStates.NoContract, impact.Steps[0].Comparison);
    }

    [Fact]
    public void Impact_TheSameEndpointInTwoSteps_CountsTheChangeInBoth()
    {
        var impact = Compute(
            [Step("compra", "POST", "/simulacoes"), Step("venda", "POST", "/simulacoes")],
            Changes(ContractChangeStates.Compared, Change(Breaking, BecameRequired, "POST", "/simulacoes", "contaOrigem")));

        Assert.Equal(2, impact.Breaking);
        Assert.Equal(2, impact.BreakingSteps);
    }

    [Fact]
    public void Impact_StepsWithChangesComeFirst()
    {
        var impact = Compute(
            [Step("j", "GET", "/a", position: 0), Step("j", "GET", "/b", position: 1)],
            Changes(ContractChangeStates.Compared, Change(Breaking, EndpointRemoved, "GET", "/b")));

        Assert.Equal(1, impact.Steps[0].Step.Position);
    }

    [Fact]
    public void AppChangeSet_UsesTheBestStateAcrossRepositoriesAndPoolsTheirChanges()
    {
        var changes = new AppContractChanges("app-1",
        [
            new RepositoryContractChanges(new Repository { Id = "r1", AppId = "app-1", Name = "r1" }, null, null, ContractChangeStates.FirstAnalysis, [], ["aviso a"]),
            new RepositoryContractChanges(new Repository { Id = "r2", AppId = "app-1", Name = "r2" }, null, null, ContractChangeStates.Compared,
                [Change(Breaking, EndpointRemoved, "GET", "/x")], ["aviso a", "aviso b"])
        ]);

        var set = AppChangeSet.From("App Um", changes);

        Assert.Equal(ContractChangeStates.Compared, set.Comparison);
        Assert.Single(set.Changes);
        Assert.Equal(["aviso a", "aviso b"], set.Warnings);
    }
}
