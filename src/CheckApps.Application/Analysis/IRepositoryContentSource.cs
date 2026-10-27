using Repository = CheckApps.Domain.Repository;

namespace CheckApps.Application.Analysis;

/// <summary>
/// Porta para ler os arquivos de um repositório **já catalogado** (spec 013). Devolve tipos nossos, nunca o JSON do
/// provedor — no molde da <see cref="Discovery.IRepositoryDiscoverySource"/>. Há duas implementações: a fake
/// (testes e demonstração) e a do GitHub.
/// </summary>
public interface IRepositoryContentSource
{
    /// <summary>
    /// O que falta para a fonte funcionar (ex.: token ou organização não configurados), ou <c>null</c> se está pronta.
    /// A mensagem nunca contém valores de configuração.
    /// </summary>
    string? ConfigurationProblem { get; }

    /// <summary>Esta fonte sabe ler o repositório (provedor e id externo)? Os que não sabe são pulados, não são erro.</summary>
    bool CanRead(Repository repository);

    /// <summary>Lista os arquivos do ramo padrão e diz em que commit isso foi visto.</summary>
    /// <exception cref="Discovery.RepositorySourceException">Sem acesso, repositório vazio, limite de chamadas, rede.</exception>
    Task<RepositoryTree> ListAsync(Repository repository, CancellationToken cancellationToken = default);

    /// <summary>Lê um arquivo no commit informado; <c>null</c> se ele não existe mais.</summary>
    Task<string?> ReadFileAsync(
        Repository repository, string commitSha, string path, CancellationToken cancellationToken = default);
}

/// <param name="Truncated">O provedor devolveu só parte da árvore (repositório enorme): a análise será parcial.</param>
public sealed record RepositoryTree(string CommitSha, IReadOnlyList<RepositoryFile> Files, bool Truncated = false);

public sealed record RepositoryFile(string Path, long Size);
