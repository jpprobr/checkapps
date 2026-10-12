using CheckApps.Application.Discovery;

namespace CheckApps.Infrastructure.Discovery;

/// <summary>
/// Fonte de demonstração e de teste: repositórios fictícios das siglas <c>ab1</c> e <c>cd2</c>, com
/// os casos difíceis de propósito. Os nomes são montados a partir do prefixo recebido, então
/// seguem qualquer convenção configurada. Mesmo comportamento da fonte real nos pontos que
/// importam: casa por trecho do nome (ruído incluído), mede o total antes de devolver e respeita o limite.
/// </summary>
public class FakeRepositorySource : IRepositoryDiscoverySource
{
    public const string ProviderName = "fake";

    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    // sigla, final do nome, sigla-app, arquivado, dias desde o último push
    private static readonly (string Sigla, string Suffix, string? SiglaApp, bool Archived, int DaysSincePush)[] Catalog =
    [
        ("ab1", "app-pagamentos", "AB1-0001", false, 2),            // aplicação típica: papel e sigla-app concordam
        ("ab1", "app-bff-pagamentos", "AB1-0002", false, 5),        // nome com mais de uma palavra
        ("ab1", "infra-fila", null, false, 30),                      // infra: sem sigla-app
        ("ab1", "contrato-api-pagamentos", null, false, 45),         // contrato OpenAPI
        ("ab1", "app-legado", null, true, 400),                      // arquivado, nome diz app mas sem sigla-app (confiança baixa)
        ("ab1", "infra-conta", "AB1-0009", false, 60),               // infra COM sigla-app (confiança baixa)
        ("ab1", "app-migrado", "CD2-0001", false, 10),               // sigla-app de outra sigla (aviso)
        ("cd2", "app-faturas", "CD2-0002", false, 1),                // outra sigla
    ];

    public string Provider => ProviderName;

    public Task<IReadOnlyList<DiscoveredRepositoryInfo>> DiscoverAsync(
        string sigla, string namePrefix, int maxResults, CancellationToken cancellationToken = default)
    {
        var wanted = sigla.Trim().ToLowerInvariant();
        var prefix = namePrefix.ToLowerInvariant();

        // O que uma busca "por trecho do nome" devolveria: os repositórios da sigla mais ruído
        // fora da convenção (que o filtro de prefixo exato descarta).
        var candidates = new List<(string Name, string? SiglaApp, bool Archived, int Days)>();
        foreach (var entry in Catalog.Where(e => e.Sigla == wanted))
        {
            candidates.Add((prefix + entry.Suffix, entry.SiglaApp, entry.Archived, entry.DaysSincePush));
        }

        if (candidates.Count > 0)
        {
            candidates.Add(($"sandbox-{prefix}ruido", null, false, 3));                  // contém o prefixo, mas não começa com ele
            candidates.Add((prefix.TrimEnd('-') + "9-app-vizinho", null, false, 3));      // sigla maior que casa por trecho
        }

        if (candidates.Count > maxResults)
        {
            throw new ScanLimitExceededException(wanted, candidates.Count, maxResults);
        }

        IReadOnlyList<DiscoveredRepositoryInfo> result = candidates
            .Where(c => c.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(c => new DiscoveredRepositoryInfo(
                ExternalId: StableId(c.Name),
                Name: c.Name,
                Url: $"https://example.invalid/fake/{c.Name}",
                Archived: c.Archived,
                PushedAt: Now.AddDays(-c.Days),
                SiglaApp: c.SiglaApp))
            .ToList();

        return Task.FromResult(result);
    }

    // Id estável entre execuções (string.GetHashCode não é): o fake se comporta como um id do provedor.
    private static string StableId(string name)
    {
        var hash = 17;
        foreach (var c in name)
        {
            hash = unchecked(hash * 31 + c);
        }

        return (1_000_000 + Math.Abs(hash % 1_000_000)).ToString();
    }
}
