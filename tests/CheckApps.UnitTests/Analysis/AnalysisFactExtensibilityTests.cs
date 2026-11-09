using CheckApps.Application.Analysis;
using CheckApps.Application.Discovery;
using CheckApps.Domain;
using CheckApps.Domain.Analysis;
using CheckApps.UnitTests.TestDoubles;

namespace CheckApps.UnitTests.Analysis;

/// <summary>
/// O que a refatoração da spec 021 promete: um tipo novo de fato não muda o <see cref="AnalysisService"/>. Tudo o que é
/// novo aqui mora no teste: saída, processador e fato.
/// </summary>
public class AnalysisFactExtensibilityTests
{
    private sealed record TestOutput(string Value) : IScannerOutput;

    private sealed record TestFact(string AnalysisId, string Value, string ScannerId) : IAnalysisFact;

    private sealed class TestProcessor : IAnalysisFactProcessor
    {
        public string Label => "coisas";

        public AnalysisFactBatch Process(ScannerResult result, AnalysisFactContext context) =>
            new(result.Outputs.OfType<TestOutput>().Select(o => new TestFact(context.AnalysisId, o.Value, context.ScannerId)).ToList<IAnalysisFact>(),
                Warnings: result.Outputs.OfType<TestOutput>().Any(o => o.Value == "ruim") ? ["coisa ruim"] : null,
                Notes: ["nota do processador"]);
    }

    private sealed class TestScanner(params string[] values) : IRepositoryScanner
    {
        public string Id => "teste";

        public string Version => "1.0.0";

        public bool AppliesTo(IReadOnlyList<string> paths) => true;

        public IReadOnlyList<string> SelectFiles(IReadOnlyList<string> paths) => [];

        public ScannerResult Analyze(IReadOnlyDictionary<string, string> files) =>
            new([], [], outputs: values.Select(v => new TestOutput(v)).ToList());
    }

    private readonly FakeAnalysisRepository _analyses = new();

    private async Task<AnalysisItem> AnalyzeAsync(params string[] values)
    {
        var apps = new FakeAppRepository();
        apps.Add(new App { Id = "app-1", SquadId = "sq-1", Name = "App" });
        await apps.AddRepositoryAsync(new Repository
        {
            Id = "r1", AppId = "app-1", Name = "repo", Provider = "fake", ExternalId = "r1", Sigla = "ab1", Status = "active"
        });

        var source = new FakeContentSource().With("r1", "sha-1", new Dictionary<string, string> { ["a.txt"] = "x" });
        var service = new AnalysisService(
            source, new ScannerRegistry([new TestScanner(values)]), [new TestProcessor()], apps, _analyses, new AnalysisOptions(),
            new ScanOptions(), new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        return Assert.Single((await service.AnalyzeAsync(["ab1"], dryRun: false)).Items);
    }

    [Fact]
    public async Task ANewKindOfFact_ReachesTheRepository_WithoutChangingTheService()
    {
        var item = await AnalyzeAsync("a", "b");

        var fact = Assert.IsType<TestFact>(_analyses.AllFacts[0]);
        Assert.Equal(2, _analyses.AllFacts.Count);
        Assert.Equal("teste", fact.ScannerId);
        Assert.Equal(_analyses.Analyses[0].Id, fact.AnalysisId);
        Assert.Equal([new AnalysisFactCount("coisas", 2)], item.FactCounts);
    }

    [Theory]
    [InlineData("a", AnalysisStatuses.Ok)]
    [InlineData("ruim", AnalysisStatuses.Partial)]
    public async Task ProcessorWarningsMakeTheAnalysisPartial_AndItsNotesDoNot(string value, string status) =>
        Assert.Equal(status, (await AnalyzeAsync(value)).Status);
}
