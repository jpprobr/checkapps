using CheckApps.Domain.Analysis;

namespace CheckApps.UnitTests.Analysis;

public class InternalPackageTests
{
    [Theory]
    [InlineData("Empresa.SDK.TokenManager", true)]
    [InlineData("empresa.sdk.tokenmanager", true)]
    [InlineData("Empresa.Contratos.Cliente", true)]
    [InlineData("Newtonsoft.Json", false)]
    [InlineData("EmpresaFoo", false)]            // prefixo termina em ponto: não casa por trecho
    [InlineData("Terceiro.Empresa.Algo", false)]  // só o começo conta
    public void IsInternal_MatchesTheNamePrefix_IgnoringCase(string name, bool expected) =>
        Assert.Equal(expected, InternalPackage.IsInternal(name, ["Empresa."]));

    [Fact]
    public void IsInternal_AcceptsAPrefixWithoutTheTrailingDot()
    {
        Assert.True(InternalPackage.IsInternal("Empresa.SDK.TokenManager", ["Empresa"]));
        Assert.False(InternalPackage.IsInternal("EmpresaFoo", ["Empresa"]));
    }

    [Fact]
    public void IsInternal_SupportsSeveralPrefixes_AndIgnoresBlankOnes()
    {
        string[] prefixes = ["", "  ", "Outra.", "Empresa."];

        Assert.True(InternalPackage.IsInternal("Outra.Biblioteca", prefixes));
        Assert.True(InternalPackage.IsInternal("Empresa.Algo", prefixes));
        Assert.False(InternalPackage.IsInternal("Serilog", prefixes));
    }

    [Fact]
    public void IsInternal_MarksNothing_WhenNoPrefixIsConfigured()
    {
        Assert.False(InternalPackage.IsInternal("Empresa.SDK.TokenManager", null));
        Assert.False(InternalPackage.IsInternal("Empresa.SDK.TokenManager", []));
        Assert.False(InternalPackage.IsInternal("Empresa.SDK.TokenManager", [""]));
    }

    [Fact]
    public void IsInternal_IsFalse_ForABlankName() =>
        Assert.False(InternalPackage.IsInternal(" ", ["Empresa."]));
}
