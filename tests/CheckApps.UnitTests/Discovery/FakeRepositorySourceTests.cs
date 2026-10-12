using CheckApps.Application.Discovery;
using CheckApps.Domain.Discovery;
using CheckApps.Infrastructure.Discovery;
using CheckApps.UnitTests.TestDoubles;

namespace CheckApps.UnitTests.Discovery;

/// <summary>A fonte fake é o que a demonstração e os roteiros de navegador usam; ela precisa mostrar os casos difíceis.</summary>
public class FakeRepositorySourceTests
{
    private readonly FakeRepositorySource _sut = new();

    [Fact]
    public async Task Discover_ReturnsTheSiglaRepositories_NamedAfterTheGivenPrefix_WithoutTheNoise()
    {
        var found = await _sut.DiscoverAsync("ab1", "lab-ab1-", maxResults: 50);

        Assert.Equal(7, found.Count);
        Assert.All(found, r => Assert.StartsWith("lab-ab1-", r.Name));
        Assert.DoesNotContain(found, r => r.Name.Contains("ruido") || r.Name.Contains("vizinho"));
    }

    [Fact]
    public async Task Discover_CoversTheHardCases()
    {
        var found = await _sut.DiscoverAsync("ab1", "lab-ab1-", 50);

        Assert.Contains(found, r => r.Archived);                                           // arquivado
        Assert.Contains(found, r => r.SiglaApp is null && r.Name.Contains("-infra-"));      // infra sem sigla-app
        Assert.Contains(found, r => r.SiglaApp is not null && r.Name.Contains("-infra-"));  // infra com sigla-app
        Assert.Contains(found, r => r.SiglaApp == "CD2-0001");                              // sigla-app de outra sigla
    }

    [Fact]
    public async Task Discover_IsStableAcrossCalls_SoRescansDoNotDuplicate()
    {
        var first = await _sut.DiscoverAsync("ab1", "lab-ab1-", 50);
        var second = await new FakeRepositorySource().DiscoverAsync("ab1", "lab-ab1-", 50);

        Assert.Equal(first.Select(r => r.ExternalId), second.Select(r => r.ExternalId));
    }

    [Fact]
    public async Task Discover_UnknownSigla_IsEmpty_AndTheLimitIsHonoured()
    {
        Assert.Empty(await _sut.DiscoverAsync("zzz", "lab-zzz-", 50));

        var error = await Assert.ThrowsAsync<ScanLimitExceededException>(() => _sut.DiscoverAsync("ab1", "lab-ab1-", maxResults: 3));
        Assert.True(error.Total > 3);
    }

    [Fact]
    public async Task EndToEnd_FakeSourceThroughTheScanService_YieldsTheExpectedSuggestions()
    {
        var discovered = new FakeDiscoveredRepositoryRepository(new FakeAppRepository());
        var service = new ScanService(_sut, discovered, new ScanOptions { NameTemplate = "lab-{sigla}-" }, TimeProvider.System);

        var report = await service.ScanAsync(["ab1", "cd2"], dryRun: false);

        Assert.Equal(8, report.Items.Count);
        var byName = report.Items.ToDictionary(i => i.Repository.Name);
        Assert.Equal(SuggestionConfidence.High, byName["lab-ab1-app-pagamentos"].Suggestion.Confidence);
        Assert.Equal(SuggestionConfidence.High, byName["lab-ab1-infra-fila"].Suggestion.Confidence);
        Assert.Equal(SuggestionConfidence.Low, byName["lab-ab1-app-legado"].Suggestion.Confidence);
        Assert.Equal(SuggestionConfidence.Low, byName["lab-ab1-infra-conta"].Suggestion.Confidence);
        Assert.Contains(report.Warnings, w => w.Contains("lab-ab1-app-migrado"));
    }
}
