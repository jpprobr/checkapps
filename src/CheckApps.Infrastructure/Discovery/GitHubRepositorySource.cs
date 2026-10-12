using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
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

    private readonly HttpClient _http;
    private readonly GitHubOptions _options;

    public GitHubRepositorySource(HttpClient http, GitHubOptions options)
    {
        _http = http;
        _options = options;

        _http.BaseAddress ??= new Uri(options.ApiBaseUrl.TrimEnd('/') + "/");

        // Sem token não há cabeçalho de autorização — e quem tentar ler recebe ConfigurationProblem.
        if (!string.IsNullOrWhiteSpace(options.Token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        }

        _http.DefaultRequestHeaders.Accept.Clear();
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        if (!_http.DefaultRequestHeaders.Contains("X-GitHub-Api-Version"))
        {
            _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        }

        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CheckApps-Scan", "1.0"));
        }
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

    private async Task<HttpResponseMessage> SendAsync(
        string relativeUrl, CancellationToken cancellationToken, bool allowNotFound = false)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(relativeUrl, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new RepositorySourceException("Não foi possível falar com o GitHub. Verifique a conexão e a URL configurada.", ex);
        }

        if (response.IsSuccessStatusCode || (allowNotFound && response.StatusCode == HttpStatusCode.NotFound))
        {
            return response;
        }

        var error = Describe(response);
        response.Dispose();
        throw error;
    }

    /// <summary>Mensagens próprias, sem corpo de resposta nem cabeçalhos da requisição: nada aqui carrega o token.</summary>
    private RepositorySourceException Describe(HttpResponseMessage response)
    {
        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
                return new RepositorySourceException("O GitHub recusou o token (inválido ou expirado). Gere outro e configure GitHub:Token.");

            case HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests when RateLimitReset(response) is { } reset:
                return new RepositorySourceException(
                    $"O limite de chamadas do GitHub foi esgotado. Ele é renovado em {reset.ToLocalTime():dd/MM/yyyy HH:mm}; tente de novo depois.");

            case HttpStatusCode.Forbidden:
                return new RepositorySourceException(
                    "O GitHub negou o acesso. Confira se o token tem Metadata e Contents (leitura) na organização e se a política da organização aceita tokens fine-grained.");

            case HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity:
                return new RepositorySourceException(
                    $"A organização '{_options.Organization}' não foi encontrada, ou o token não tem acesso a ela.");

            default:
                return new RepositorySourceException(
                    $"O GitHub respondeu com erro ({(int)response.StatusCode}). Tente de novo em instantes.");
        }
    }

    private static DateTimeOffset? RateLimitReset(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) || remaining.FirstOrDefault() != "0")
        {
            return null;
        }

        return response.Headers.TryGetValues("X-RateLimit-Reset", out var reset)
            && long.TryParse(reset.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : DateTimeOffset.UtcNow.AddMinutes(1);
    }

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
