namespace CheckApps.Domain.Analysis;

/// <summary>
/// Decisão **humana** de que um host é de um serviço **externo** ao catálogo (spec 019): outra squad, terceiro ou sistema
/// legado que ninguém vai cadastrar como app. Tira o host da fila "sem app" sem inventar um app. Como o vínculo
/// (<see cref="HostLink"/>), vive fora das análises e a chave é <c>host[:porta]</c> em minúsculas, podendo ser um modelo
/// com <c>{env}</c>.
/// </summary>
public sealed record HostExternal
{
    public required string Id { get; init; }
    public required string HostKey { get; init; }

    /// <summary>Nome livre do serviço ("API de pagamentos da squad X"); opcional.</summary>
    public string? Name { get; init; }

    /// <summary>Um de <see cref="HostLinkOrigins"/>.</summary>
    public string Origin { get; init; } = HostLinkOrigins.Human;

    public required DateTimeOffset DecidedAt { get; init; }
    public string? Note { get; init; }
}
