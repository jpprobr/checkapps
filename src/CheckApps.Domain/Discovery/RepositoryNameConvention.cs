using System.Globalization;

namespace CheckApps.Domain.Discovery;

/// <summary>
/// Convenção de nome dos repositórios (ex.: <c>{prefixo}-{sigla}-{segmento}-{nome}</c>), vinda
/// de configuração. Função pura e sem estado: o prefixo exato serve de filtro do scan, e o que
/// vem depois dele sugere o papel e o nome do app. Sugestões nunca são armazenadas — só o que
/// a pessoa confirma na triagem vira dado do catálogo.
/// </summary>
public sealed class RepositoryNameConvention
{
    public const string SiglaPlaceholder = "{sigla}";

    private static readonly Dictionary<string, string> RoleBySegment = new(StringComparer.OrdinalIgnoreCase)
    {
        ["app"] = RepositoryRoles.Application,
        ["infra"] = RepositoryRoles.Infra,
        ["contrato"] = RepositoryRoles.Contract,
        ["contract"] = RepositoryRoles.Contract,
        ["secrets"] = RepositoryRoles.Secrets,
        ["secret"] = RepositoryRoles.Secrets
    };

    private readonly string _template;

    /// <param name="template">Deve conter <c>{sigla}</c> (ex.: <c>lab-{sigla}-</c>).</param>
    public RepositoryNameConvention(string template)
    {
        if (string.IsNullOrWhiteSpace(template) || !template.Contains(SiglaPlaceholder, StringComparison.Ordinal))
        {
            throw new ArgumentException($"A convenção de nome deve conter {SiglaPlaceholder} (ex.: lab-{SiglaPlaceholder}-).", nameof(template));
        }

        _template = template.Trim();
    }

    /// <summary>Prefixo exato de uma sigla, em minúsculas.</summary>
    public string PrefixFor(string sigla) =>
        _template.Replace(SiglaPlaceholder, sigla.Trim(), StringComparison.Ordinal).ToLowerInvariant();

    /// <summary>O nome começa exatamente com o prefixo da sigla (sem diferenciar caixa)?</summary>
    public bool Matches(string repositoryName, string sigla) =>
        repositoryName.StartsWith(PrefixFor(sigla), StringComparison.OrdinalIgnoreCase)
        && repositoryName.Length > PrefixFor(sigla).Length;

    /// <summary>Papel, nome do app e confiança sugeridos. Só faz sentido para nomes que passam em <see cref="Matches"/>.</summary>
    public RepositorySuggestion Suggest(string repositoryName, string sigla, string? siglaApp)
    {
        // Tolerante: sem o prefixo (ex.: convenção não configurada), o nome inteiro é o "resto".
        var rest = Matches(repositoryName, sigla) ? repositoryName[PrefixFor(sigla).Length..] : repositoryName;
        var firstDash = rest.IndexOf('-');
        var segment = firstDash > 0 ? rest[..firstDash] : null;

        string? role = null;
        var appPart = rest;
        if (segment is not null && RoleBySegment.TryGetValue(segment, out var knownRole))
        {
            role = knownRole;
            appPart = rest[(firstDash + 1)..];
        }

        var hasSiglaApp = !string.IsNullOrWhiteSpace(siglaApp);

        // Repositório de aplicação tem sigla-app; os demais papéis, não. Quando nome e sigla-app
        // discordam, a sugestão sai com confiança baixa — sinal de que a pessoa precisa olhar.
        var confidence = role switch
        {
            RepositoryRoles.Application => hasSiglaApp ? SuggestionConfidence.High : SuggestionConfidence.Low,
            null => SuggestionConfidence.Low,
            _ => hasSiglaApp ? SuggestionConfidence.Low : SuggestionConfidence.High
        };

        return new RepositorySuggestion(role, ToDisplayName(appPart), confidence);
    }

    /// <summary>Sugestão quando não há convenção configurada: só o nome, sem papel, com confiança baixa.</summary>
    public static RepositorySuggestion WithoutConvention(string repositoryName) =>
        new(null, ToDisplayName(repositoryName), SuggestionConfidence.Low);

    private static string ToDisplayName(string value)
    {
        var words = value.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', words.Select(w => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(w.ToLowerInvariant())));
    }
}

public enum SuggestionConfidence
{
    High,
    Low
}

/// <summary>Sugestão calculada na leitura. <see cref="Role"/> é nulo quando o segmento do nome não é conhecido.</summary>
public sealed record RepositorySuggestion(string? Role, string Name, SuggestionConfidence Confidence);

/// <summary>A sigla é o trecho inicial da sigla-app (<c>AB1</c> em <c>AB1-0001</c>) — certo numa squad, provável na organização.</summary>
public static class SiglaAppConvention
{
    public static string? SiglaOf(string? siglaApp)
    {
        if (string.IsNullOrWhiteSpace(siglaApp))
        {
            return null;
        }

        var dash = siglaApp.IndexOf('-');
        return (dash > 0 ? siglaApp[..dash] : siglaApp).Trim().ToLowerInvariant();
    }

    /// <summary>Divergência gera aviso, nunca erro.</summary>
    public static bool Matches(string? siglaApp, string sigla) =>
        SiglaOf(siglaApp) is { } derived && string.Equals(derived, sigla.Trim(), StringComparison.OrdinalIgnoreCase);
}
