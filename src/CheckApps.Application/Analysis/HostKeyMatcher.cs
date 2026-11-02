using System.Text.RegularExpressions;
using CheckApps.Domain.Analysis;

namespace CheckApps.Application.Analysis;

/// <summary>
/// Regras puras das chaves de host (spec 017): validação e casamento do modelo <c>empresa.{env}.exemplo.com</c> com o host
/// concreto. O <c>{env}</c> só assume os valores configurados (<c>dev</c>, <c>hom</c>, <c>prod</c> por padrão): nunca por
/// palpite. Sem diferença de caixa.
/// </summary>
public static partial class HostKeyMatcher
{
    public const string Placeholder = "{env}";

    /// <summary>Como o vínculo chegou ao host: o próprio host ou um modelo que o cobre.</summary>
    public const string ViaExact = "exact";

    public const string ViaTemplate = "template";

    [GeneratedRegex(@"^[a-z0-9]([a-z0-9._-]*[a-z0-9])?(:[0-9]{1,5})?$")]
    private static partial Regex KeyShape();

    /// <summary>Normaliza (aparar e minúsculas) e valida <c>host[:porta]</c>, com <c>{env}</c> permitido no host.</summary>
    /// <exception cref="ArgumentException">Vazia, com esquema, caminho, espaço, chave solta ou porta fora de 1–65535.</exception>
    public static string Normalize(string? hostKey)
    {
        var key = (hostKey ?? "").Trim().ToLowerInvariant();
        if (key.Length == 0)
        {
            throw new ArgumentException("Informe o host (host ou host:porta).");
        }

        // {env} é a única chave permitida; trocado por um texto neutro, o resto precisa ser um host comum.
        var shape = key.Replace(Placeholder, "x", StringComparison.Ordinal);
        if (!KeyShape().IsMatch(shape))
        {
            throw new ArgumentException($"Host inválido: \"{hostKey}\". Use host ou host:porta, sem esquema, caminho ou espaço.");
        }

        var colon = key.LastIndexOf(':');
        if (colon > 0 && (!int.TryParse(key[(colon + 1)..], out var port) || port is < 1 or > 65535))
        {
            throw new ArgumentException($"Porta inválida em \"{hostKey}\" (use 1 a 65535).");
        }

        return key;
    }

    public static bool IsTemplate(string hostKey) => hostKey.Contains(Placeholder, StringComparison.Ordinal);

    /// <summary>
    /// O vínculo de chave <paramref name="linkKey"/> cobre o host <paramref name="hostKey"/>? Igualdade exata, ou o
    /// <paramref name="linkKey"/> é modelo e algum valor de <paramref name="envValues"/> no lugar de <c>{env}</c> o iguala
    /// (a porta faz parte da chave, então tem de ser a mesma). Um host que é ele mesmo modelo casa só por igualdade.
    /// </summary>
    public static string? Cover(string linkKey, string hostKey, IReadOnlyList<string> envValues)
    {
        if (string.Equals(linkKey, hostKey, StringComparison.OrdinalIgnoreCase))
        {
            return ViaExact;
        }

        if (!IsTemplate(linkKey) || IsTemplate(hostKey))
        {
            return null;
        }

        return envValues.Any(v => string.Equals(
                linkKey.Replace(Placeholder, v.Trim(), StringComparison.Ordinal), hostKey, StringComparison.OrdinalIgnoreCase))
            ? ViaTemplate
            : null;
    }
}

/// <summary>Um vínculo que vale para um host, com o app já nomeado e o caminho (<c>exact</c> ou <c>template</c>).</summary>
/// <param name="Template">A chave do modelo quando <paramref name="Via"/> é <c>template</c>; nula no exato.</param>
public record HostLinkMatch(
    string LinkId, string HostKey, string AppId, string AppName, string State, string Via, string? Template,
    DateTimeOffset DecidedAt, string? Note);

/// <summary>O que vale para um host: o app confirmado (se houver) e os apps rejeitados.</summary>
public record HostLinkInfo(HostLinkMatch? Confirmed, IReadOnlyList<HostLinkMatch> Rejected)
{
    public static readonly HostLinkInfo None = new(null, []);

    /// <summary><c>confirmado</c>, <c>rejeitado</c> (só rejeitos) ou <c>sem-vinculo</c>; é o que o filtro <c>linkStatus</c> compara.</summary>
    public string Status =>
        Confirmed is not null ? LinkStatuses.Confirmed : Rejected.Count > 0 ? LinkStatuses.Rejected : LinkStatuses.None;
}

public static class LinkStatuses
{
    public const string None = "sem-vinculo";
    public const string Confirmed = HostLinkStates.Confirmed;
    public const string Rejected = HostLinkStates.Rejected;

    public static readonly string[] All = [None, Confirmed, Rejected];
}

/// <summary>
/// Resolve, para qualquer host, o vínculo efetivo entre todos os vínculos gravados. Dentro do mesmo estado o **exato**
/// vence o modelo; vínculos do mesmo tipo desempatam pela chave, para o resultado ser estável.
/// </summary>
public class HostLinkResolver(
    IReadOnlyList<HostLink> links, IReadOnlyDictionary<string, string> appNames, IReadOnlyList<string> envValues)
{
    public IReadOnlyList<HostLink> Links => links;

    public HostLinkInfo Resolve(string hostKey)
    {
        var matches = new List<HostLinkMatch>();
        foreach (var link in links.OrderBy(l => l.HostKey, StringComparer.Ordinal).ThenBy(l => l.AppId, StringComparer.Ordinal))
        {
            if (HostKeyMatcher.Cover(link.HostKey, hostKey, envValues) is not { } via)
            {
                continue;
            }

            matches.Add(new HostLinkMatch(
                link.Id, link.HostKey, link.AppId, appNames.GetValueOrDefault(link.AppId, link.AppId), link.State, via,
                via == HostKeyMatcher.ViaTemplate ? link.HostKey : null, link.DecidedAt, link.Note));
        }

        var confirmed = matches
            .Where(m => m.State == HostLinkStates.Confirmed)
            .OrderBy(m => m.Via == HostKeyMatcher.ViaExact ? 0 : 1)
            .FirstOrDefault();

        // Um rejeito do mesmo app que o confirmado efetivo não é mostrado: o confirmado vale.
        var rejected = matches
            .Where(m => m.State == HostLinkStates.Rejected && m.AppId != confirmed?.AppId)
            .OrderBy(m => m.AppName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new HostLinkInfo(confirmed, rejected);
    }
}
