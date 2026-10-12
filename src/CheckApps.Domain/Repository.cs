namespace CheckApps.Domain;

public class Repository
{
    public required string Id { get; init; }
    public required string AppId { get; init; }
    public required string Name { get; set; }
    public string? Provider { get; set; }
    public string? Url { get; set; }
    public string? ExternalId { get; set; }
    public string? Status { get; set; }

    /// <summary>Agrupador corporativo (ex.: conta AWS). Repositórios de infra também têm.</summary>
    public string? Sigla { get; set; }

    /// <summary>
    /// Código gerado pela esteira corporativa para cada repositório de aplicação. Identifica o
    /// repositório, não o app. Único quando presente; repositórios de infra não têm.
    /// </summary>
    public string? SiglaApp { get; set; }

    /// <summary>Para que serve o repositório dentro do app. Texto livre, como <see cref="Status"/>; ver <see cref="Discovery.RepositoryRoles"/>.</summary>
    public string? Role { get; set; }

    /// <summary>Proveniência: <see cref="Discovery.RepositorySources"/>. Nulo nos dados anteriores à spec 005.</summary>
    public string? Source { get; set; }
}
