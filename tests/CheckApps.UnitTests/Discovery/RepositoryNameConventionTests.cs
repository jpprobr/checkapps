using CheckApps.Domain.Discovery;

namespace CheckApps.UnitTests.Discovery;

public class RepositoryNameConventionTests
{
    private readonly RepositoryNameConvention _sut = new("lab-{sigla}-");

    [Fact]
    public void Constructor_RequiresTheSiglaPlaceholder()
    {
        Assert.Throws<ArgumentException>(() => new RepositoryNameConvention("lab-"));
        Assert.Throws<ArgumentException>(() => new RepositoryNameConvention(" "));
    }

    [Fact]
    public void PrefixFor_BuildsTheLowercaseExactPrefix()
    {
        Assert.Equal("lab-ab1-", _sut.PrefixFor("AB1"));
    }

    [Theory]
    [InlineData("lab-ab1-app-pagamentos", true)]
    [InlineData("LAB-AB1-App-Pagamentos", true)]
    [InlineData("lab-ab1-", false)]                 // só o prefixo, sem nome
    [InlineData("lab-ab12-app-pagamentos", false)]   // sigla maior que casa por substring
    [InlineData("xlab-ab1-app-pagamentos", false)]
    [InlineData("playground", false)]
    public void Matches_RequiresTheExactPrefix(string name, bool expected)
    {
        Assert.Equal(expected, _sut.Matches(name, "ab1"));
    }

    [Fact]
    public void Suggest_ApplicationWithSiglaApp_HighConfidence()
    {
        var suggestion = _sut.Suggest("lab-ab1-app-pagamentos", "ab1", "AB1-0001");

        Assert.Equal(RepositoryRoles.Application, suggestion.Role);
        Assert.Equal("Pagamentos", suggestion.Name);
        Assert.Equal(SuggestionConfidence.High, suggestion.Confidence);
    }

    [Fact]
    public void Suggest_KeepsTheRestOfTheNameAfterTheSegment()
    {
        var suggestion = _sut.Suggest("lab-ab1-app-bff-pagamentos", "ab1", "AB1-0002");

        Assert.Equal("Bff Pagamentos", suggestion.Name);
    }

    [Theory]
    [InlineData("lab-ab1-infra-fila", RepositoryRoles.Infra)]
    [InlineData("lab-ab1-contrato-api-pagamentos", RepositoryRoles.Contract)]
    [InlineData("lab-ab1-secrets-pagamentos", RepositoryRoles.Secrets)]
    public void Suggest_NonApplicationRoleWithoutSiglaApp_HighConfidence(string name, string expectedRole)
    {
        var suggestion = _sut.Suggest(name, "ab1", siglaApp: null);

        Assert.Equal(expectedRole, suggestion.Role);
        Assert.Equal(SuggestionConfidence.High, suggestion.Confidence);
    }

    [Fact]
    public void Suggest_ApplicationNameButNoSiglaApp_LowConfidence()
    {
        var suggestion = _sut.Suggest("lab-ab1-app-legado", "ab1", siglaApp: null);

        Assert.Equal(RepositoryRoles.Application, suggestion.Role);
        Assert.Equal(SuggestionConfidence.Low, suggestion.Confidence);
    }

    [Fact]
    public void Suggest_InfraNameButWithSiglaApp_LowConfidence()
    {
        var suggestion = _sut.Suggest("lab-ab1-infra-fila", "ab1", "AB1-0003");

        Assert.Equal(RepositoryRoles.Infra, suggestion.Role);
        Assert.Equal(SuggestionConfidence.Low, suggestion.Confidence);
    }

    [Theory]
    [InlineData("lab-ab1-legado")]                // sem segmento
    [InlineData("lab-ab1-xpto-coisa")]            // segmento desconhecido
    public void Suggest_UnknownSegment_NoRole_KeepsTheWholeRestAsName(string name)
    {
        var suggestion = _sut.Suggest(name, "ab1", siglaApp: null);

        Assert.Null(suggestion.Role);
        Assert.Equal(SuggestionConfidence.Low, suggestion.Confidence);
        Assert.Equal(name == "lab-ab1-legado" ? "Legado" : "Xpto Coisa", suggestion.Name);
    }

    [Theory]
    [InlineData("AB1-0001", "ab1")]
    [InlineData("ab1-9", "ab1")]
    [InlineData("AB1", "ab1")]
    [InlineData(null, null)]
    [InlineData("  ", null)]
    public void SiglaAppConvention_SiglaOf_IsTheLeadingPart(string? siglaApp, string? expected)
    {
        Assert.Equal(expected, SiglaAppConvention.SiglaOf(siglaApp));
    }

    [Fact]
    public void SiglaAppConvention_Matches_IsCaseInsensitive_AndFalseWhenAbsentOrDifferent()
    {
        Assert.True(SiglaAppConvention.Matches("AB1-0001", "ab1"));
        Assert.False(SiglaAppConvention.Matches("CD2-0001", "ab1"));
        Assert.False(SiglaAppConvention.Matches(null, "ab1"));
    }
}
