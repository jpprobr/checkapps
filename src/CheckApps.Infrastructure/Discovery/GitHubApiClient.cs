using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using CheckApps.Application.Discovery;

namespace CheckApps.Infrastructure.Discovery;

/// <summary>
/// O que a descoberta (<see cref="GitHubRepositorySource"/>) e a leitura de conteúdo (<see cref="GitHubRepositoryContentSource"/>)
/// têm em comum: cabeçalhos, chamada GET e a tradução de erro HTTP em mensagem nossa.
///
/// Segurança: o token só viaja no cabeçalho Authorization. Nenhuma mensagem de erro o inclui, nem o corpo da resposta
/// nem cabeçalhos da requisição — as mensagens são todas montadas aqui.
/// </summary>
internal sealed class GitHubApiClient
{
    private readonly HttpClient _http;

    public GitHubApiClient(HttpClient http, GitHubOptions options)
    {
        _http = http;

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

    /// <param name="allowNotFound">404 é um resultado normal (ex.: repositório sem o arquivo), não um erro.</param>
    /// <param name="messages">
    /// Mensagem própria para 404, 409 e 422, que significam coisas diferentes conforme a chamada. Os demais códigos
    /// (401, 403, 429 e o resto) têm sempre a mesma tradução.
    /// </param>
    /// <exception cref="RepositorySourceException">Rede ou resposta de erro, já com mensagem saneada.</exception>
    public async Task<HttpResponseMessage> GetAsync(
        string relativeUrl, CancellationToken cancellationToken, bool allowNotFound = false,
        IReadOnlyDictionary<HttpStatusCode, string>? messages = null)
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

        var error = Describe(response, messages);
        response.Dispose();
        throw error;
    }

    /// <summary>Mensagens próprias, sem corpo de resposta nem cabeçalhos da requisição: nada aqui carrega o token.</summary>
    private static RepositorySourceException Describe(HttpResponseMessage response, IReadOnlyDictionary<HttpStatusCode, string>? messages)
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

            default:
                if (messages is not null && messages.TryGetValue(response.StatusCode, out var message))
                {
                    return new RepositorySourceException(message);
                }

                return response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity
                    ? new RepositorySourceException("O recurso não foi encontrado, ou o token não tem acesso a ele.")
                    : new RepositorySourceException(
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
}
