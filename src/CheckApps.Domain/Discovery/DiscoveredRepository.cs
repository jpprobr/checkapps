namespace CheckApps.Domain.Discovery;

/// <summary>
/// Repositório encontrado pelo leitor de GitHub e ainda sem destino no catálogo (spec 005).
/// O scan nunca cria App: ele alimenta esta caixa de entrada, e a pessoa decide.
/// A relação com o catálogo é derivada — o repositório está no catálogo quando existe um
/// <see cref="Repository"/> com o mesmo provedor e id externo.
/// </summary>
public class DiscoveredRepository
{
    public required string Id { get; init; }
    public required string Provider { get; init; }
    public required string ExternalId { get; init; }
    public required string Name { get; set; }
    public string? Url { get; set; }
    public required string Sigla { get; set; }
    public string? SiglaApp { get; set; }
    public bool Archived { get; set; }
    public DateTimeOffset? PushedAt { get; set; }
    public TriageStatus Status { get; set; } = TriageStatus.New;
    public required DateTimeOffset FirstSeenAt { get; init; }
    public required DateTimeOffset LastSeenAt { get; set; }
    public required string Source { get; init; }

    /// <summary>Os dados observados (não a triagem nem as datas de visto) são os mesmos?</summary>
    public bool SameObservation(DiscoveredRepository other) =>
        Name == other.Name && Url == other.Url && Sigla == other.Sigla && SiglaApp == other.SiglaApp
        && Archived == other.Archived && PushedAt == other.PushedAt;

    /// <summary>Identidade estável: usa o id do provedor, que sobrevive a renomeações do repositório.</summary>
    public static string IdFor(string provider, string externalId) => $"{provider}:{externalId}";
}

/// <summary>
/// Só o que não dá para derivar do catálogo. "Já no catálogo" não é um status — é a existência
/// de um Repository —, o que evita inconsistência quando um app é removido.
/// </summary>
public enum TriageStatus
{
    New,
    Ignored,
    OtherSquad
}

/// <summary>Códigos textuais de <see cref="TriageStatus"/> — o que a API e o banco usam.</summary>
public static class TriageCodes
{
    public static string ToCode(this TriageStatus status) => status switch
    {
        TriageStatus.New => "new",
        TriageStatus.Ignored => "ignored",
        TriageStatus.OtherSquad => "other-squad",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static TriageStatus ParseStatus(string code) =>
        TryParseStatus(code, out var status)
            ? status
            : throw new ArgumentException($"Status de triagem desconhecido: \"{code}\".");

    public static bool TryParseStatus(string? code, out TriageStatus status)
    {
        foreach (var candidate in Enum.GetValues<TriageStatus>())
        {
            if (candidate.ToCode() == code)
            {
                status = candidate;
                return true;
            }
        }

        status = default;
        return false;
    }
}

/// <summary>Valores de <see cref="Repository.Source"/>.</summary>
public static class RepositorySources
{
    public const string GithubScan = "GITHUB_SCAN";
    public const string Manual = "MANUAL";
}

/// <summary>Papéis conhecidos de um repositório. Lista aberta: o campo é texto livre.</summary>
public static class RepositoryRoles
{
    public const string Application = "aplicacao";
    public const string Infra = "infra";
    public const string Contract = "contrato";
    public const string Secrets = "secrets";
}
