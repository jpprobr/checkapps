using System.Text.RegularExpressions;

namespace CheckApps.Domain.Discovery;

/// <summary>
/// O que um scan pode ler: uma lista de siglas validadas. Nunca texto livre de busca e nunca a
/// organização inteira — entrada vazia, ou fora do formato configurado, é recusada antes de
/// qualquer consulta externa (spec 005, FR-002).
/// </summary>
public sealed record ScanScope(IReadOnlyList<string> Siglas)
{
    /// <summary>Normaliza (minúsculas, sem repetição) e valida cada sigla contra <paramref name="siglaPattern"/>.</summary>
    /// <exception cref="ArgumentException">Nenhuma sigla informada, ou alguma fora do formato.</exception>
    public static ScanScope Create(IEnumerable<string>? siglas, string siglaPattern)
    {
        var normalized = (siglas ?? [])
            .Select(s => s?.Trim().ToLowerInvariant() ?? "")
            .Where(s => s.Length > 0)
            .Distinct()
            .ToList();

        if (normalized.Count == 0)
        {
            throw new ArgumentException("Informe ao menos uma sigla. O scan nunca lê a organização inteira.");
        }

        var pattern = new Regex(siglaPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        var invalid = normalized.Where(s => !pattern.IsMatch(s)).ToList();
        if (invalid.Count > 0)
        {
            throw new ArgumentException(
                $"Sigla fora do formato esperado ({siglaPattern}): {string.Join(", ", invalid)}.");
        }

        return new ScanScope(normalized);
    }
}
