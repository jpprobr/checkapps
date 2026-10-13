using CheckApps.Application.Persistence;
using CheckApps.Domain.Discovery;

namespace CheckApps.Application.Discovery;

/// <summary>
/// O scan propõe, a pessoa decide: lê os repositórios das siglas e os grava na caixa de
/// entrada. **Nunca cria App.** Toda a leitura acontece antes de qualquer gravação, para que uma
/// falha na segunda sigla não deixe um scan pela metade.
/// </summary>
public class ScanService(
    IRepositoryDiscoverySource source, IDiscoveredRepositoryRepository discovered, ScanOptions options, TimeProvider timeProvider)
{
    /// <summary>
    /// O que impede o scan de rodar (convenção de nome ausente, ou a fonte sem credencial), ou
    /// <c>null</c> se está pronto. Sem valores de configuração — a tela mostra este texto.
    /// </summary>
    public string? ConfigurationProblem =>
        string.IsNullOrWhiteSpace(options.NameTemplate)
            ? $"Configure {ScanOptions.SectionName}:{nameof(ScanOptions.NameTemplate)} com a convenção de nome dos repositórios (ex.: {{prefixo}}-{{sigla}}-)."
            : source.ConfigurationProblem;

    /// <exception cref="ArgumentException">Nenhuma sigla, ou fora do formato — antes de consultar a fonte.</exception>
    /// <exception cref="InvalidOperationException">Convenção de nome ou credencial não configuradas.</exception>
    /// <exception cref="ScanLimitExceededException">Alguma busca passaria do limite de resultados.</exception>
    public async Task<ScanReport> ScanAsync(
        IEnumerable<string>? siglas, bool dryRun, CancellationToken cancellationToken = default)
    {
        var scope = ScanScope.Create(siglas, options.SiglaPattern);

        if (ConfigurationProblem is { } problem)
        {
            throw new InvalidOperationException(problem);
        }

        var convention = new RepositoryNameConvention(options.NameTemplate!);
        var now = timeProvider.GetUtcNow();
        var warnings = new List<string>();
        var found = new Dictionary<string, (DiscoveredRepository Repository, RepositorySuggestion Suggestion)>();

        foreach (var sigla in scope.Siglas)
        {
            var infos = await source.DiscoverAsync(sigla, convention.PrefixFor(sigla), options.MaxResults, cancellationToken);

            // A fonte já filtra, mas o prefixo exato é regra nossa (FR-003), não de quem implementa a porta.
            foreach (var info in infos.Where(i => convention.Matches(i.Name, sigla)))
            {
                var repository = new DiscoveredRepository
                {
                    Id = DiscoveredRepository.IdFor(source.Provider, info.ExternalId),
                    Provider = source.Provider,
                    ExternalId = info.ExternalId,
                    Name = info.Name,
                    Url = info.Url,
                    Sigla = sigla,
                    SiglaApp = info.SiglaApp,
                    Archived = info.Archived,
                    PushedAt = info.PushedAt,
                    FirstSeenAt = now,
                    LastSeenAt = now,
                    Source = RepositorySources.GithubScan
                };

                if (info.Warning is not null)
                {
                    warnings.Add($"{info.Name}: {info.Warning}");
                }

                // Divergência entre sigla e sigla-app é aviso, nunca erro: certo numa squad, provável na organização.
                if (info.SiglaApp is not null && !SiglaAppConvention.Matches(info.SiglaApp, sigla))
                {
                    warnings.Add($"{info.Name}: a sigla-app '{info.SiglaApp}' não começa com a sigla '{sigla}'.");
                }

                found[repository.Id] = (repository, convention.Suggest(info.Name, sigla, info.SiglaApp));
            }
        }

        var items = new List<ScanItem>();
        foreach (var (repository, suggestion) in found.Values)
        {
            var outcome = dryRun
                ? await ClassifyAsync(repository, cancellationToken)
                : await discovered.UpsertAsync(repository, cancellationToken);
            items.Add(new ScanItem(repository, outcome, suggestion));
        }

        // "Já no catálogo" é uma informação a mais, não um quarto resultado: o outcome continua descrevendo
        // os dados observados. Uma consulta em lote para o resultado inteiro, no scan real e na simulação.
        var catalog = await discovered.GetCatalogAppsAsync(
            source.Provider, items.Select(i => i.Repository.ExternalId).ToList(), cancellationToken);
        var marked = items
            .Select(i => catalog.TryGetValue(i.Repository.ExternalId, out var app) ? i with { CatalogApp = app } : i)
            .ToList();

        return new ScanReport(dryRun, marked, warnings);
    }

    /// <summary>No modo de simulação: o que o upsert devolveria, sem gravar.</summary>
    private async Task<UpsertOutcome> ClassifyAsync(DiscoveredRepository repository, CancellationToken cancellationToken)
    {
        var existing = await discovered.GetAsync(repository.Id, cancellationToken);
        if (existing is null)
        {
            return UpsertOutcome.New;
        }

        return existing.SameObservation(repository) ? UpsertOutcome.Unchanged : UpsertOutcome.Updated;
    }
}

/// <summary>O app em que um repositório já está no catálogo (um Repository com o mesmo provedor e id externo).</summary>
public record CatalogApp(string AppId, string AppName);

/// <param name="CatalogApp">Preenchido quando o repositório já virou app — e por isso não aparece na caixa de entrada.</param>
public record ScanItem(
    DiscoveredRepository Repository, UpsertOutcome Outcome, RepositorySuggestion Suggestion, CatalogApp? CatalogApp = null);

public record ScanReport(bool DryRun, IReadOnlyList<ScanItem> Items, IReadOnlyList<string> Warnings)
{
    public int Count(UpsertOutcome outcome) => Items.Count(i => i.Outcome == outcome);

    /// <summary>Quantos itens do resultado já estão no catálogo. Sobrepõe-se às contagens por resultado, não as soma.</summary>
    public int InCatalogCount => Items.Count(i => i.CatalogApp is not null);
}
