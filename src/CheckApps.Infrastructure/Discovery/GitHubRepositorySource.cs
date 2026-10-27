using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using CheckApps.Application.Discovery;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace CheckApps.Infrastructure.Discovery;

/// <summary>
/// Lê repositórios do GitHub pela Search API (<c>org:{org} {sigla} in:name</c>) — nunca lista a
/// organização inteira — e a sigla-app do arquivo YAML de cada repositório que passa no prefixo.
/// O total da busca é medido antes de qualquer outra leitura (limite de resultados).
///
/// Segurança: o token só viaja no cabeçalho Authorization. Nenhuma mensagem de erro, daqui ou da
/// exceção, o inclui — as mensagens são todas montadas por esta classe. Nenhum teste usa rede.
/// </summary>
public class GitHubRepositorySource : IRepositoryDiscoverySource
{
    private const int PerPage = 100;
    private const int MaxSearchPages = 10;   // a Search API devolve no máximo 1000 resultados

    private readonly GitHubApiClient _api;
    private readonly GitHubOptions _options;

    public GitHubRepositorySource(HttpClient http, GitHubOptions options)
    {
        _api = new GitHubApiClient(http, options);
        _options = options;
    }

    public string Provider => "github";

    /// <summary>Falta token ou organização? A mensagem diz como configurar e nunca traz valores.</summary>
    public string? ConfigurationProblem =>
        string.IsNullOrWhiteSpace(_options.Token)
            ? "Configure GitHub:Token (ex.: dotnet user-secrets set \"GitHub:Token\" \"<token>\"). A ferramenta não roda sem token."
            : string.IsNullOrWhiteSpace(_options.Organization)
                ? "Configure GitHub:Organization (ex.: dotnet user-secrets set \"GitHub:Organization\" \"<organização>\")."
                : null;

    public async Task<IReadOnlyList<DiscoveredRepositoryInfo>> DiscoverAsync(
        string sigla, string namePrefix, int maxResults, CancellationToken cancellationToken = default)
    {
        if (ConfigurationProblem is { } problem)
        {
            throw new InvalidOperationException(problem);
        }

        var hits = await SearchAsync(sigla, maxResults, cancellationToken);

        var result = new List<DiscoveredRepositoryInfo>();
        foreach (var hit in hits.Where(h => h.Name.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase)))
        {
            var (siglaApp, warning) = await ReadSiglaAppAsync(hit.FullName, cancellationToken);
            result.Add(new DiscoveredRepositoryInfo(hit.Id, hit.Name, hit.Url, hit.Archived, hit.PushedAt, siglaApp, warning));
        }

        return result;
    }

    private async Task<List<SearchHit>> SearchAsync(string sigla, int maxResults, CancellationToken cancellationToken)
    {
        var query = Uri.EscapeDataString($"{sigla} in:name org:{_options.Organization}");
        var hits = new List<SearchHit>();

        for (var page = 1; page <= MaxSearchPages; page++)
        {
            using var response = await SendAsync(
                $"search/repositories?q={query}&per_page={PerPage}&page={page}", cancellationToken);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;

            // O limite é checado na primeira resposta, antes de ler mais páginas e antes de qualquer YAML.
            var total = root.GetProperty("total_count").GetInt32();
            if (total > maxResults)
            {
                throw new ScanLimitExceededException(sigla, total, maxResults);
            }

            var items = root.GetProperty("items");
            foreach (var item in items.EnumerateArray())
            {
                hits.Add(SearchHit.From(item));
            }

            if (hits.Count >= total || items.GetArrayLength() == 0)
            {
                break;
            }
        }

        return hits;
    }

    /// <summary>404 (sem arquivo) é o caso normal de repositório sem sigla-app; YAML inválido vira aviso, não erro.</summary>
    private async Task<(string? SiglaApp, string? Warning)> ReadSiglaAppAsync(string fullName, CancellationToken cancellationToken)
    {
        var path = string.Join('/', _options.ConfigFilePath.Split('/').Select(Uri.EscapeDataString));
        using var response = await SendAsync($"repos/{fullName}/contents/{path}", cancellationToken, allowNotFound: true);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return (null, null);
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;

        // Um diretório volta como lista, não como arquivo: não há o que ler.
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("content", out var content)
            || content.ValueKind != JsonValueKind.String)
        {
            return (null, null);
        }

        string yaml;
        try
        {
            yaml = Encoding.UTF8.GetString(Convert.FromBase64String(content.GetString()!.Replace("\n", "")));
        }
        catch (FormatException)
        {
            return (null, $"o arquivo {_options.ConfigFilePath} não pôde ser decodificado");
        }

        return ExtractSiglaApp(yaml);
    }

    private (string? SiglaApp, string? Warning) ExtractSiglaApp(string yaml)
    {
        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(yaml));

            if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode map)
            {
                return (null, null);
            }

            foreach (var (key, value) in map.Children)
            {
                if (key is YamlScalarNode k && string.Equals(k.Value, _options.SiglaAppKey, StringComparison.OrdinalIgnoreCase))
                {
                    return value is YamlScalarNode v && !string.IsNullOrWhiteSpace(v.Value)
                        ? (v.Value.Trim(), null)
                        : (null, $"a chave '{_options.SiglaAppKey}' de {_options.ConfigFilePath} não tem um valor simples");
                }
            }

            return (null, null);
        }
        catch (YamlException)
        {
            return (null, $"o arquivo {_options.ConfigFilePath} não é um YAML válido");
        }
    }

    /// <summary>404 e 422 aqui significam organização inexistente ou sem acesso (ou, no 404 do arquivo, só "sem sigla-app").</summary>
    private Task<HttpResponseMessage> SendAsync(string relativeUrl, CancellationToken cancellationToken, bool allowNotFound = false) =>
        _api.GetAsync(relativeUrl, cancellationToken, allowNotFound, new Dictionary<HttpStatusCode, string>
        {
            [HttpStatusCode.NotFound] = $"A organização '{_options.Organization}' não foi encontrada, ou o token não tem acesso a ela.",
            [HttpStatusCode.UnprocessableEntity] = $"A organização '{_options.Organization}' não foi encontrada, ou o token não tem acesso a ela."
        });

    private sealed record SearchHit(string Id, string Name, string FullName, string? Url, bool Archived, DateTimeOffset? PushedAt)
    {
        public static SearchHit From(JsonElement item) => new(
            Id: item.GetProperty("id").GetInt64().ToString(CultureInfo.InvariantCulture),
            Name: item.GetProperty("name").GetString()!,
            FullName: item.GetProperty("full_name").GetString()!,
            Url: item.TryGetProperty("html_url", out var url) ? url.GetString() : null,
            Archived: item.TryGetProperty("archived", out var archived) && archived.GetBoolean(),
            PushedAt: item.TryGetProperty("pushed_at", out var pushed) && pushed.ValueKind == JsonValueKind.String
                ? DateTimeOffset.Parse(pushed.GetString()!, CultureInfo.InvariantCulture)
                : null);
    }
}
