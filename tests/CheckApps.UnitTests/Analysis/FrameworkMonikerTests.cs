using CheckApps.Domain.Analysis;

namespace CheckApps.UnitTests.Analysis;

public class FrameworkMonikerTests
{
    [Theory]
    [InlineData("v4.8", "net48")]
    [InlineData("v4.7.2", "net472")]
    [InlineData("v4.0", "net40")]
    [InlineData("V4.6.1", "net461")]
    [InlineData("  v4.8  ", "net48")]
    public void FromTargetFrameworkVersion_NormalizesTheLegacyFormat(string value, string expected) =>
        Assert.Equal(expected, FrameworkMoniker.FromTargetFrameworkVersion(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("4.8")]
    [InlineData("net48")]
    [InlineData("v4")]
    [InlineData("v4.x")]
    [InlineData("$(Versao)")]
    public void FromTargetFrameworkVersion_ReturnsNull_WhenTheValueIsNotInTheLegacyFormat(string? value) =>
        Assert.Null(FrameworkMoniker.FromTargetFrameworkVersion(value));
}
