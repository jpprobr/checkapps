using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CheckApps.Domain;

public static partial class Slug
{
    public static string Generate(string name)
    {
        var normalized = name.Normalize(NormalizationForm.FormD);
        var withoutDiacritics = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                withoutDiacritics.Append(c);
            }
        }

        var lowered = withoutDiacritics.ToString().ToLowerInvariant();
        var dashed = NonAlphanumeric().Replace(lowered, "-");
        var collapsed = MultipleDashes().Replace(dashed, "-");
        return collapsed.Trim('-');
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();

    [GeneratedRegex("-{2,}")]
    private static partial Regex MultipleDashes();
}
