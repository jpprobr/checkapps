using CheckApps.Domain.Discovery;

namespace CheckApps.UnitTests.Discovery;

public class ScanScopeTests
{
    private const string Pattern = "^[a-z0-9]{3}$";

    [Fact]
    public void Create_NormalizesToLowercase_TrimsAndRemovesDuplicates()
    {
        var scope = ScanScope.Create([" AB1", "cd2 ", "ab1"], Pattern);

        Assert.Equal(["ab1", "cd2"], scope.Siglas);
    }

    [Theory]
    [MemberData(nameof(EmptyInputs))]
    public void Create_RejectsNoSiglas(IEnumerable<string>? siglas)
    {
        var error = Assert.Throws<ArgumentException>(() => ScanScope.Create(siglas, Pattern));

        Assert.Contains("nunca lê a organização inteira", error.Message);
    }

    [Theory]
    [InlineData("lab-")]     // o caso "itau-": um trecho de nome, não uma sigla
    [InlineData("ab")]
    [InlineData("abcd")]
    [InlineData("ab 1")]
    [InlineData("a_1")]
    public void Create_RejectsSiglasOutsideTheConfiguredFormat(string sigla)
    {
        var error = Assert.Throws<ArgumentException>(() => ScanScope.Create(["ab1", sigla], Pattern));

        Assert.Contains(sigla.Trim().ToLowerInvariant(), error.Message);
    }

    public static IEnumerable<object?[]> EmptyInputs() =>
    [
        [null],
        [Array.Empty<string>()],
        [new[] { "", "  " }]
    ];
}
