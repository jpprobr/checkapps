using System.Globalization;
using CheckApps.Domain.Analysis;

namespace CheckApps.Application.Analysis;

/// <summary>
/// O núcleo da redação (spec 014, FR-004, ADR-011): transforma os candidatos **brutos** de um scanner nas integrações que
/// podem ser gravadas. Só saem daqui as partes normalizadas do endereço (esquema, host, porta, caminho base): usuário,
/// senha, query e fragmento são descartados, e **o valor bruto nunca vai para aviso, nota ou log**. Fica aqui, e não em
/// cada scanner, para nenhum scanner futuro esquecer.
///
/// Regras: só <c>http</c>/<c>https</c>; <c>localhost</c>/<c>127.0.0.1</c>/<c>::1</c> são ignorados; valor vazio ou com
/// referência de substituição (<c>${VAR}</c>, <c>#{Var}#</c>, <c>%VAR%</c>) é "não resolvida", sem host; <c>{env}</c> no
/// host vira um **modelo** (estado "parametrizada", confiança média), sem substituir por palpite.
/// </summary>
public static class IntegrationNormalizer
{
    private static readonly string[] LocalHosts = ["localhost", "127.0.0.1", "::1"];

    public static NormalizedIntegrations Normalize(
        IEnumerable<ScannerIntegration> candidates, string analysisId, string scannerId, string scannerVersion,
        IReadOnlyDictionary<string, string[]> environmentAliases, Func<string> newId)
    {
        var items = new List<RepositoryIntegration>();
        var warnings = new List<string>();
        var ignoredLocal = 0;
        var ignoredNotHttp = 0;
        var seen = new HashSet<(string File, string Key)>();

        foreach (var candidate in candidates)
        {
            if (!seen.Add((candidate.SourceFile, candidate.ConfigKey)))
            {
                continue;
            }

            var parsed = Parse(candidate.RawTarget);
            switch (parsed.Outcome)
            {
                case ParseOutcome.Local:
                    ignoredLocal++;
                    continue;
                case ParseOutcome.NotHttp:
                    ignoredNotHttp++;
                    continue;
                case ParseOutcome.Unresolved:
                    warnings.Add($"{candidate.SourceFile}: {candidate.ConfigKey}: {parsed.Reason}; integração não resolvida.");
                    break;
            }

            var environmentKind = EnvironmentNames.Classify(candidate.EnvironmentName, environmentAliases);
            var common = new RepositoryIntegration
            {
                Id = newId(),
                AnalysisId = analysisId,
                Mechanism = candidate.Mechanism,
                Reference = candidate.Reference,
                ConfigKey = candidate.ConfigKey,
                Environment = candidate.EnvironmentName,
                EnvironmentKind = environmentKind,
                ProjectPath = candidate.ProjectPath,
                SourceFile = candidate.SourceFile,
                ScannerId = scannerId,
                ScannerVersion = scannerVersion,
                State = IntegrationStates.Resolved,
                Confidence = Confidences.High
            };

            items.Add(parsed.Outcome switch
            {
                ParseOutcome.Unresolved => common with
                {
                    State = IntegrationStates.Unresolved,
                    Confidence = Confidences.Low,
                    ConfidenceNote = parsed.Reason
                },
                ParseOutcome.Template => common with
                {
                    Scheme = parsed.Scheme,
                    Host = parsed.Host,
                    Port = parsed.Port,
                    BasePath = parsed.BasePath,
                    State = IntegrationStates.Parameterized,
                    Confidence = Confidences.Medium,
                    ConfidenceNote = "o host depende do ambiente ({env}); guardado como modelo"
                },
                _ => common with { Scheme = parsed.Scheme, Host = parsed.Host, Port = parsed.Port, BasePath = parsed.BasePath }
            });
        }

        var notes = new List<string>();
        if (ignoredLocal + ignoredNotHttp > 0)
        {
            var parts = new List<string>();
            if (ignoredLocal > 0)
            {
                parts.Add($"{ignoredLocal} {(ignoredLocal == 1 ? "local" : "locais")}");
            }

            if (ignoredNotHttp > 0)
            {
                parts.Add($"{ignoredNotHttp} sem URL http(s)");
            }

            var total = ignoredLocal + ignoredNotHttp;
            notes.Add($"{total} {(total == 1 ? "valor ignorado" : "valores ignorados")} nas configurações ({string.Join(", ", parts)}).");
        }

        return new NormalizedIntegrations(items, warnings, notes);
    }

    private enum ParseOutcome { Resolved, Template, Unresolved, Local, NotHttp }

    private sealed record Parsed(
        ParseOutcome Outcome, string? Scheme = null, string? Host = null, int? Port = null, string? BasePath = null, string? Reason = null);

    private static Parsed Parse(string raw)
    {
        var value = raw.Trim();
        if (value.Length == 0)
        {
            return new Parsed(ParseOutcome.Unresolved, Reason: "valor vazio");
        }

        if (HasSubstitution(value))
        {
            return new Parsed(ParseOutcome.Unresolved, Reason: "valor com referência de substituição (variável ou token)");
        }

        var schemeEnd = value.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd <= 0)
        {
            return new Parsed(ParseOutcome.NotHttp);
        }

        var scheme = value[..schemeEnd].ToLowerInvariant();
        if (scheme is not ("http" or "https"))
        {
            return new Parsed(ParseOutcome.NotHttp);
        }

        var rest = value[(schemeEnd + 3)..];

        // Fragmento e query são descartados antes de qualquer outra coisa.
        var cut = rest.IndexOfAny(['?', '#']);
        if (cut >= 0)
        {
            rest = rest[..cut];
        }

        var slash = rest.IndexOf('/');
        var authority = slash < 0 ? rest : rest[..slash];
        var path = slash < 0 ? "" : rest[slash..];

        // Usuário e senha: tudo até o último '@' é descartado.
        var at = authority.LastIndexOf('@');
        if (at >= 0)
        {
            authority = authority[(at + 1)..];
        }

        var (host, port) = SplitHostPort(authority);
        if (host.Length == 0 || !IsValidHost(host))
        {
            return new Parsed(ParseOutcome.Unresolved, Reason: "endereço sem host reconhecível");
        }

        host = host.ToLowerInvariant();
        if (LocalHosts.Contains(host.Trim('[', ']')))
        {
            return new Parsed(ParseOutcome.Local);
        }

        var basePath = path.TrimEnd('/');
        var defaultPort = scheme == "https" ? 443 : 80;
        int? explicitPort = port is { } p && p != defaultPort ? p : null;
        var outcome = host.Contains('{') ? ParseOutcome.Template : ParseOutcome.Resolved;

        return new Parsed(outcome, scheme, host, explicitPort, basePath.Length == 0 ? null : basePath);
    }

    private static bool HasSubstitution(string value) =>
        value.Contains("${", StringComparison.Ordinal)
        || value.Contains("#{", StringComparison.Ordinal)
        || value.Contains("{{", StringComparison.Ordinal)
        || (value.Length > 2 && value[0] == '%' && value.IndexOf('%', 1) > 0);

    private static (string Host, int? Port) SplitHostPort(string authority)
    {
        if (authority.StartsWith('['))
        {
            var close = authority.IndexOf(']');
            if (close > 0)
            {
                var ipv6 = authority[..(close + 1)];
                var tail = authority[(close + 1)..];
                return (ipv6, tail.StartsWith(':') ? ParsePort(tail[1..]) : null);
            }
        }

        var colon = authority.LastIndexOf(':');
        return colon < 0 ? (authority, null) : (authority[..colon], ParsePort(authority[(colon + 1)..]));
    }

    private static int? ParsePort(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is > 0 and < 65536 ? port : null;

    private static bool IsValidHost(string host) =>
        host.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' or '{' or '}' or '[' or ']' or ':');
}

/// <param name="Warnings">Avisos de verdade (tornam a análise parcial); nunca trazem o valor bruto.</param>
/// <param name="Notes">Informativas (quantos valores foram ignorados e por quê); não tornam a análise parcial.</param>
public sealed record NormalizedIntegrations(
    IReadOnlyList<RepositoryIntegration> Items, IReadOnlyList<string> Warnings, IReadOnlyList<string> Notes);
