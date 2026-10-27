using System.Text.RegularExpressions;

namespace CheckApps.Domain.Analysis;

/// <summary>
/// Normaliza o <c>TargetFrameworkVersion</c> do .NET Framework (projeto fora do formato SDK) para o
/// mesmo moniker dos projetos novos: <c>v4.8</c> → <c>net48</c>, <c>v4.7.2</c> → <c>net472</c>, <c>v4.0</c> → <c>net40</c>.
/// </summary>
public static partial class FrameworkMoniker
{
    [GeneratedRegex(@"^[vV](\d+(?:\.\d+){1,3})$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex VersionPattern();

    /// <returns>O moniker, ou <c>null</c> se o valor não segue o padrão <c>vN.N[.N]</c>.</returns>
    public static string? FromTargetFrameworkVersion(string? value)
    {
        var match = VersionPattern().Match(value?.Trim() ?? string.Empty);
        return match.Success ? "net" + match.Groups[1].Value.Replace(".", string.Empty) : null;
    }
}
