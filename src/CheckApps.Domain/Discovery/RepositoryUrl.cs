namespace CheckApps.Domain.Discovery;

/// <summary>
/// Comparação de URLs de repositório (spec 012): sem espaços nas pontas, sem barra final e sem diferença de
/// maiúsculas. Não trata o sufixo <c>.git</c> nem <c>http</c> versus <c>https</c>. O SQL equivalente
/// (<c>lower(rtrim(trim(url), '/'))</c>) fica em <c>CatalogMatchSql</c>; os dois precisam andar juntos.
/// </summary>
public static class RepositoryUrl
{
    /// <summary>A forma comparável da URL, ou <c>null</c> se ela estiver vazia.</summary>
    public static string? Normalize(string? url)
    {
        var normalized = url?.Trim().TrimEnd('/').ToLowerInvariant();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    public static bool Same(string? a, string? b) =>
        Normalize(a) is { } left && left == Normalize(b);
}
