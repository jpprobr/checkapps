using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using CheckApps.Application.Analysis;
using CheckApps.Application.Discovery;
using CheckApps.Infrastructure.Discovery;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.Infrastructure.Analysis;

/// <summary>
/// Lê os arquivos de um repositório do GitHub **já catalogado** (spec 013), identificado pelo id externo — que sobrevive
/// a renomeações. Quatro chamadas por repositório mais uma por arquivo de interesse:
/// <c>repositories/{id}</c> (nome completo e ramo padrão) → <c>commits/{ramo}</c> (sha) → <c>git/trees/{sha}?recursive=1</c>
/// (a lista de arquivos) → <c>contents/{caminho}?ref={sha}</c> (só o que o scanner pediu).
///
/// Segurança: mesmo <see cref="GitHubApiClient"/> da descoberta — o token só viaja no cabeçalho Authorization e nenhuma
/// mensagem de erro, daqui ou da exceção, o inclui. Nenhum teste usa rede.
/// </summary>
public class GitHubRepositoryContentSource : IRepositoryContentSource
{
    public const string ProviderName = "github";

    private readonly GitHubApiClient _api;
    private readonly GitHubOptions _options;
    private readonly ConcurrentDictionary<string, string> _fullNames = new();

    public GitHubRepositoryContentSource(HttpClient http, GitHubOptions options)
    {
        _api = new GitHubApiClient(http, options);
        _options = options;
    }

    /// <summary>Só o token é necessário: o repositório é lido pelo id, não pela organização.</summary>
    public string? ConfigurationProblem =>
        string.IsNullOrWhiteSpace(_options.Token)
            ? "Configure GitHub:Token (ex.: dotnet user-secrets set \"GitHub:Token\" \"<token>\"). A ferramenta não roda sem token."
            : null;

    public bool CanRead(Repository repository) =>
        string.Equals(repository.Provider, ProviderName, StringComparison.OrdinalIgnoreCase)
        && long.TryParse(repository.ExternalId, NumberStyles.None, CultureInfo.InvariantCulture, out _);

    public async Task<RepositoryTree> ListAsync(Repository repository, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        string fullName, branch;
        using (var response = await _api.GetAsync(
            $"repositories/{repository.ExternalId}", cancellationToken,
            messages: new Dictionary<HttpStatusCode, string>
            {
                [HttpStatusCode.NotFound] = "O repositório não foi encontrado no GitHub, ou o token não tem acesso a ele."
            }))
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            fullName = document.RootElement.GetProperty("full_name").GetString()!;
            branch = document.RootElement.TryGetProperty("default_branch", out var b) && b.ValueKind == JsonValueKind.String
                ? b.GetString()!
                : "HEAD";
        }

        _fullNames[repository.ExternalId!] = fullName;

        string sha;
        using (var response = await _api.GetAsync(
            $"repos/{fullName}/commits/{EscapePath(branch)}", cancellationToken,
            messages: new Dictionary<HttpStatusCode, string>
            {
                [HttpStatusCode.Conflict] = "O repositório está vazio: não há commit para analisar.",
                [HttpStatusCode.NotFound] = "O ramo padrão do repositório não foi encontrado.",
                [HttpStatusCode.UnprocessableEntity] = "O ramo padrão do repositório não foi encontrado."
            }))
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            sha = document.RootElement.GetProperty("sha").GetString()!;
        }

        using var treeResponse = await _api.GetAsync(
            $"repos/{fullName}/git/trees/{sha}?recursive=1", cancellationToken,
            messages: new Dictionary<HttpStatusCode, string>
            {
                [HttpStatusCode.NotFound] = "A árvore de arquivos do repositório não foi encontrada.",
                [HttpStatusCode.Conflict] = "O repositório está vazio: não há arquivos para analisar."
            });
        using var tree = JsonDocument.Parse(await treeResponse.Content.ReadAsStringAsync(cancellationToken));

        var files = new List<RepositoryFile>();
        if (tree.RootElement.TryGetProperty("tree", out var entries) && entries.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.TryGetProperty("type", out var type) && type.GetString() == "blob"
                    && entry.TryGetProperty("path", out var path) && path.ValueKind == JsonValueKind.String)
                {
                    files.Add(new RepositoryFile(
                        path.GetString()!, entry.TryGetProperty("size", out var size) && size.TryGetInt64(out var bytes) ? bytes : 0));
                }
            }
        }

        var truncated = tree.RootElement.TryGetProperty("truncated", out var t) && t.ValueKind == JsonValueKind.True;
        return new RepositoryTree(sha, files, truncated);
    }

    public async Task<string?> ReadFileAsync(
        Repository repository, string commitSha, string path, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var fullName = _fullNames.TryGetValue(repository.ExternalId!, out var known)
            ? known
            : await ResolveFullNameAsync(repository, cancellationToken);

        using var response = await _api.GetAsync(
            $"repos/{fullName}/contents/{EscapePath(path)}?ref={Uri.EscapeDataString(commitSha)}", cancellationToken,
            allowNotFound: true);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;

        // Um diretório volta como lista, e um arquivo grande volta sem conteúdo: nos dois casos não há texto a ler.
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("content", out var content)
            || content.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(content.GetString()!.Replace("\n", "")));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private async Task<string> ResolveFullNameAsync(Repository repository, CancellationToken cancellationToken)
    {
        using var response = await _api.GetAsync($"repositories/{repository.ExternalId}", cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var fullName = document.RootElement.GetProperty("full_name").GetString()!;
        _fullNames[repository.ExternalId!] = fullName;
        return fullName;
    }

    private void EnsureConfigured()
    {
        if (ConfigurationProblem is { } problem)
        {
            throw new InvalidOperationException(problem);
        }
    }

    /// <summary>Escapa cada segmento do caminho, preservando as barras (ramos e arquivos podem ter espaços e acentos).</summary>
    private static string EscapePath(string path) =>
        string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
}
