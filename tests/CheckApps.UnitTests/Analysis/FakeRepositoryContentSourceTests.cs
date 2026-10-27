using CheckApps.Application.Analysis;
using CheckApps.Domain;
using CheckApps.Domain.Analysis;
using CheckApps.Infrastructure.Analysis;
using CheckApps.Infrastructure.Analysis.DotNet;

namespace CheckApps.UnitTests.Analysis;

/// <summary>A fonte de demonstração: cada final de nome do scan fake mostra uma situação diferente da análise.</summary>
public class FakeRepositoryContentSourceTests
{
    private readonly FakeRepositoryContentSource _source = new();
    private readonly DotNetScanner _scanner = new();

    private static Repository Repo(string suffix, string provider = "fake") =>
        new() { Id = suffix, AppId = "app-1", Name = $"fake-ab1-{suffix}", Provider = provider, ExternalId = suffix };

    private async Task<ScannerResult> AnalyzeAsync(string suffix)
    {
        var repository = Repo(suffix);
        var tree = await _source.ListAsync(repository);
        var paths = tree.Files.Select(f => f.Path).ToList();
        var files = new Dictionary<string, string>();
        foreach (var path in _scanner.SelectFiles(paths))
        {
            files[path] = (await _source.ReadFileAsync(repository, tree.CommitSha, path))!;
        }

        return _scanner.Analyze(files);
    }

    [Fact]
    public void ItReadsOnlyTheFakeProvider()
    {
        Assert.True(_source.CanRead(Repo("app-pagamentos")));
        Assert.False(_source.CanRead(Repo("app-pagamentos", provider: "github")));
        Assert.Null(_source.ConfigurationProblem);
    }

    [Fact]
    public async Task TheCommitIsStable_AndChangesOnlyWithTheRevision()
    {
        var first = await _source.ListAsync(Repo("app-pagamentos"));
        var again = await _source.ListAsync(Repo("app-pagamentos"));
        var other = await _source.ListAsync(Repo("app-migrado"));
        var revised = await new FakeRepositoryContentSource(revision: 1).ListAsync(Repo("app-pagamentos"));

        Assert.Equal(first.CommitSha, again.CommitSha);
        Assert.NotEqual(first.CommitSha, other.CommitSha);
        Assert.NotEqual(first.CommitSha, revised.CommitSha);
    }

    [Theory]
    [InlineData("app-pagamentos", AnalysisStatuses.Ok, "net9.0", "net8.0")]
    [InlineData("app-migrado", AnalysisStatuses.Ok, "net8.0")]
    [InlineData("app-legado", AnalysisStatuses.Ok, "net48")]
    [InlineData("app-bff-pagamentos", AnalysisStatuses.Partial, "net6.0", "net8.0")]   // propriedade não resolvida
    [InlineData("app-faturas", AnalysisStatuses.Partial, "net8.0")]                    // um projeto inválido
    public async Task TheDemoRepositories_ShowTheirSituation(string suffix, string expectedStatus, params string[] frameworks)
    {
        var result = await AnalyzeAsync(suffix);

        Assert.Equal(frameworks.Order(), result.Facts.Where(f => f.Kind == FactKinds.Framework).Select(f => f.Name).Distinct().Order());
        Assert.Equal(expectedStatus == AnalysisStatuses.Partial, result.Warnings.Count > 0);
    }

    [Theory]
    [InlineData("infra-fila")]
    [InlineData("infra-conta")]
    [InlineData("contrato-api-pagamentos")]
    [InlineData("qualquer-outro-final")]
    public async Task RepositoriesWithoutDotNet_HaveNoApplicableScanner(string suffix)
    {
        var tree = await _source.ListAsync(Repo(suffix));

        Assert.False(_scanner.AppliesTo(tree.Files.Select(f => f.Path).ToList()));
        Assert.NotEmpty(tree.Files);
    }

    [Fact]
    public async Task TheCentralPackageDemo_HasAnInternalPackage_ForThePrefixDemonstration()
    {
        var result = await AnalyzeAsync("app-pagamentos");

        Assert.Contains(result.Facts, f => InternalPackage.IsInternal(f.Name, ["Empresa."]));
        Assert.DoesNotContain(result.Facts, f => f.Name == "Serilog" && InternalPackage.IsInternal(f.Name, ["Empresa."]));
    }

    [Theory]
    [InlineData("app-pagamentos", 3, 0, true)]       // base ({env} e um host fixo) e Production; o localhost é ignorado
    [InlineData("app-bff-pagamentos", 3, 1, false)]  // base ({env} e um ${...} não resolvido) e Staging
    [InlineData("app-migrado", 0, 0, false)]
    public async Task TheDemoRepositories_ShowTheirDeclaredIntegrations(string suffix, int recorded, int unresolved, bool hasNote)
    {
        var result = await AnalyzeAsync(suffix);

        var normalized = IntegrationNormalizer.Normalize(
            result.Integrations, "an", "dotnet", "1.1.0", EnvironmentNames.DefaultAliases, () => Guid.NewGuid().ToString("n"));

        Assert.Equal(recorded, normalized.Items.Count);
        Assert.Equal(unresolved, normalized.Items.Count(i => i.State == IntegrationStates.Unresolved));
        Assert.Equal(hasNote, normalized.Notes.Count > 0);
    }

    [Fact]
    public async Task ReadingAFileThatDoesNotExist_IsNull() =>
        Assert.Null(await _source.ReadFileAsync(Repo("app-migrado"), "x", "nao/existe.csproj"));
}
