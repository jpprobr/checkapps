namespace CheckApps.Application.Discovery;

/// <summary>
/// Porta para descobrir repositórios de uma sigla (spec 005). Devolve um tipo nosso, nunca o
/// JSON de um provedor — mesmo molde de <see cref="Sonar.ISonarProvider"/>. Há duas
/// implementações: o fake (testes e demonstração) e o GitHub (organização de teste hoje, a
/// corporativa depois, só por configuração).
/// </summary>
public interface IRepositoryDiscoverySource
{
    /// <summary>Identifica o provedor nos ids e no catálogo (ex.: <c>github</c>).</summary>
    string Provider { get; }

    /// <summary>
    /// Repositórios cujo nome começa com <paramref name="namePrefix"/> (a sigla já está no prefixo).
    /// A fonte DEVE medir o total de resultados da busca **antes** de ler dados em volume e lançar
    /// <see cref="ScanLimitExceededException"/> se passar de <paramref name="maxResults"/>.
    /// Repositório que o acesso não enxerga simplesmente não aparece — acesso parcial é o normal.
    /// </summary>
    Task<IReadOnlyList<DiscoveredRepositoryInfo>> DiscoverAsync(
        string sigla, string namePrefix, int maxResults, CancellationToken cancellationToken = default);
}

/// <summary>O que o scan observou num repositório — sem nada específico do provedor.</summary>
/// <param name="ExternalId">Id do repositório no provedor; sobrevive a renomeações.</param>
/// <param name="SiglaApp">Lida do arquivo de configuração do repositório; ausente = sem sigla-app.</param>
/// <param name="Warning">Algo que a pessoa deve saber (ex.: arquivo de configuração inválido); não impede o scan.</param>
public record DiscoveredRepositoryInfo(
    string ExternalId, string Name, string? Url, bool Archived, DateTimeOffset? PushedAt,
    string? SiglaApp, string? Warning = null);

/// <summary>A busca encontraria mais repositórios que o limite: nada é lido, a pessoa refina o escopo.</summary>
public class ScanLimitExceededException(string sigla, int total, int limit) : Exception(
    $"A busca pela sigla '{sigla}' encontraria {total} repositórios, acima do limite de {limit}. Refine o escopo; nada foi lido.")
{
    public string Sigla { get; } = sigla;
    public int Total { get; } = total;
    public int Limit { get; } = limit;
}
