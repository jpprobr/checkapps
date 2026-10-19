namespace CheckApps.Infrastructure.Sqlite;

/// <summary>
/// A regra de "já está no catálogo" (spec 012): um Repository com o mesmo provedor e id externo do descoberto
/// OU a mesma URL (ver <c>RepositoryUrl</c>). Aliases: <c>r</c> = repositories, <c>d</c> = discovered_repositories.
/// </summary>
internal static class CatalogMatchSql
{
    public const string Condition =
        """
        ((r.provider = d.provider AND r.external_id = d.external_id)
         OR (d.url IS NOT NULL AND r.url IS NOT NULL AND trim(d.url) <> ''
             AND lower(rtrim(trim(r.url), '/')) = lower(rtrim(trim(d.url), '/'))))
        """;
}
