using System.Net;
using System.Text;
using System.Text.Json;
using CheckApps.Application.Discovery;
using CheckApps.Infrastructure.Discovery;

namespace CheckApps.UnitTests.Discovery;

/// <summary>Nenhum teste usa rede: o <see cref="HttpMessageHandler"/> responde com JSON fixo no formato da API do GitHub.</summary>
public class GitHubRepositorySourceTests
{
    // Valor reconhecível, só para provar que ele nunca aparece em nenhuma mensagem.
    private const string Token = "token-falso-SEGREDO-DE-TESTE-123";
    private const string Org = "lab-org";

    private readonly StubHandler _handler = new();

    private GitHubRepositorySource Sut(Action<GitHubOptions>? configure = null)
    {
        var options = new GitHubOptions { Organization = Org, Token = Token };
        configure?.Invoke(options);
        return new GitHubRepositorySource(new HttpClient(_handler), options);
    }

    [Fact]
    public async Task Discover_SearchesByOrgAndSigla_SendsTheTokenAsBearer_AndReadsTheYamlOnlyForPrefixMatches()
    {
        _handler.OnSearch(Search(3,
            Repo(1, "lab-ab1-app-pagamentos"),
            Repo(2, "lab-ab1-infra-fila"),
            Repo(3, "sandbox-ab1-ruido")));                          // casa por trecho, não pelo prefixo
        _handler.OnContents("lab-ab1-app-pagamentos", Yaml("siglaApp: AB1-0001"));
        _handler.OnContentsNotFound("lab-ab1-infra-fila");

        var found = await Sut().DiscoverAsync("ab1", "lab-ab1-", maxResults: 50);

        Assert.Equal(["lab-ab1-app-pagamentos", "lab-ab1-infra-fila"], found.Select(f => f.Name));
        Assert.Equal("AB1-0001", found[0].SiglaApp);
        Assert.Null(found[1].SiglaApp);
        Assert.Equal("1", found[0].ExternalId);

        var search = Assert.Single(_handler.Requests, r => r.Path.StartsWith("/search/repositories"));
        Assert.Contains("org%3Alab-org", search.Query);
        Assert.Contains("ab1%20in%3Aname", search.Query);
        Assert.All(_handler.Requests, r => Assert.Equal($"Bearer {Token}", r.Authorization));
        Assert.DoesNotContain(_handler.Requests, r => r.Path.Contains("sandbox-ab1-ruido"));
    }

    [Fact]
    public async Task Discover_ReadsArchivedAndLastPush()
    {
        _handler.OnSearch(Search(1, Repo(7, "lab-ab1-app-legado", archived: true, pushedAt: "2025-03-04T05:06:07Z")));
        _handler.OnContentsNotFound("lab-ab1-app-legado");

        var found = Assert.Single(await Sut().DiscoverAsync("ab1", "lab-ab1-", 50));

        Assert.True(found.Archived);
        Assert.Equal(new DateTimeOffset(2025, 3, 4, 5, 6, 7, TimeSpan.Zero), found.PushedAt);
        Assert.Equal("https://github.invalid/lab-org/lab-ab1-app-legado", found.Url);
    }

    [Theory]
    [InlineData("siglaApp: AB1-0001", "AB1-0001", null)]
    [InlineData("SIGLAAPP:   AB1-0002  ", "AB1-0002", null)]
    [InlineData("outra: chave", null, null)]
    [InlineData("siglaApp: [a, b]", null, "valor simples")]
    [InlineData("siglaApp: \"x\n  - : [", null, "YAML válido")]
    public async Task Discover_ParsesTheSiglaAppFromYaml_AndWarnsWhenItCannot(string yaml, string? siglaApp, string? warningFragment)
    {
        _handler.OnSearch(Search(1, Repo(1, "lab-ab1-app-x")));
        _handler.OnContents("lab-ab1-app-x", Yaml(yaml));

        var found = Assert.Single(await Sut().DiscoverAsync("ab1", "lab-ab1-", 50));

        Assert.Equal(siglaApp, found.SiglaApp);
        if (warningFragment is null)
        {
            Assert.Null(found.Warning);
        }
        else
        {
            Assert.Contains(warningFragment, found.Warning);
        }
    }

    [Fact]
    public async Task Discover_ReadsTheConfiguredFilePath_AndOnlyTopLevelKeys()
    {
        _handler.OnSearch(Search(1, Repo(1, "lab-ab1-app-x")));
        _handler.OnContents("lab-ab1-app-x", Yaml("app:\n  codigo: AB1-0042"), path: "deploy/meta.yaml");

        var found = Assert.Single(await Sut(o =>
        {
            o.ConfigFilePath = "deploy/meta.yaml";
            o.SiglaAppKey = "codigo";
        }).DiscoverAsync("ab1", "lab-ab1-", 50));

        // chave aninhada não é lida: só a raiz do mapa — e isso não é erro
        Assert.Null(found.SiglaApp);
        Assert.Contains(_handler.Requests, r => r.Path.EndsWith("/contents/deploy/meta.yaml"));
    }

    [Fact]
    public async Task Discover_DirectoryInsteadOfFile_MeansNoSiglaApp()
    {
        _handler.OnSearch(Search(1, Repo(1, "lab-ab1-app-x")));
        _handler.OnContentsRaw("lab-ab1-app-x", "[{\"name\":\"a.yaml\"}]");

        var found = Assert.Single(await Sut().DiscoverAsync("ab1", "lab-ab1-", 50));

        Assert.Null(found.SiglaApp);
        Assert.Null(found.Warning);
    }

    [Fact]
    public async Task Discover_WhenTheTotalExceedsTheLimit_ThrowsBeforeReadingAnythingElse()
    {
        _handler.OnSearch(Search(120, Repo(1, "lab-ab1-app-x")));

        var error = await Assert.ThrowsAsync<ScanLimitExceededException>(() => Sut().DiscoverAsync("ab1", "lab-ab1-", maxResults: 50));

        Assert.Equal(120, error.Total);
        Assert.Equal(50, error.Limit);
        Assert.Single(_handler.Requests);        // só a busca; nenhum conteúdo foi lido
    }

    [Fact]
    public async Task Discover_PaginatesTheSearch_WhenTheLimitAllowsMoreThanOnePage()
    {
        var page1 = Enumerable.Range(1, 100).Select(i => Repo(i, $"lab-ab1-app-{i}")).ToArray();
        var page2 = Enumerable.Range(101, 50).Select(i => Repo(i, $"lab-ab1-app-{i}")).ToArray();
        _handler.OnSearchPage(1, Search(150, page1));
        _handler.OnSearchPage(2, Search(150, page2));
        foreach (var repo in page1.Concat(page2))
        {
            _handler.OnContentsNotFound(repo.Name);
        }

        var found = await Sut().DiscoverAsync("ab1", "lab-ab1-", maxResults: 200);

        Assert.Equal(150, found.Count);
        Assert.Equal(2, _handler.Requests.Count(r => r.Path.StartsWith("/search/repositories")));
    }

    [Theory]
    [InlineData(401, null, "recusou o token")]
    [InlineData(403, "reset", "limite de chamadas")]
    [InlineData(429, "reset", "limite de chamadas")]
    [InlineData(403, null, "negou o acesso")]
    [InlineData(422, null, "não foi encontrada")]
    [InlineData(404, null, "não foi encontrada")]
    [InlineData(500, null, "erro (500)")]
    public async Task Discover_MapsHttpErrorsToFriendlyMessages_ThatNeverContainTheToken(int status, string? rateLimit, string expected)
    {
        _handler.OnSearchStatus((HttpStatusCode)status, rateLimit is null ? null : ("0", "1893456000"));

        var error = await Assert.ThrowsAsync<RepositorySourceException>(() => Sut().DiscoverAsync("ab1", "lab-ab1-", 50));

        Assert.Contains(expected, error.Message);
        Assert.DoesNotContain(Token, error.Message);
        Assert.DoesNotContain(Token, error.ToString());
    }

    [Fact]
    public async Task Discover_NetworkFailure_IsFriendly_AndDoesNotLeakTheToken()
    {
        _handler.FailWith = new HttpRequestException("falha de conexão simulada");

        var error = await Assert.ThrowsAsync<RepositorySourceException>(() => Sut().DiscoverAsync("ab1", "lab-ab1-", 50));

        Assert.Contains("Não foi possível falar com o GitHub", error.Message);
        Assert.DoesNotContain(Token, error.ToString());
    }

    [Fact]
    public void Constructor_RefusesToRunWithoutTokenOrOrganization_AndSaysHowToConfigure()
    {
        var noToken = Assert.Throws<InvalidOperationException>(() =>
            new GitHubRepositorySource(new HttpClient(_handler), new GitHubOptions { Organization = Org }));
        var noOrg = Assert.Throws<InvalidOperationException>(() =>
            new GitHubRepositorySource(new HttpClient(_handler), new GitHubOptions { Token = Token }));

        Assert.Contains("GitHub:Token", noToken.Message);
        Assert.Contains("user-secrets", noToken.Message);
        Assert.Contains("GitHub:Organization", noOrg.Message);
        Assert.DoesNotContain(Token, noOrg.Message);
    }

    // ---- helpers: JSON no formato da API do GitHub ----

    private record RepoJson(long Id, string Name, bool Archived, string PushedAt);

    private static RepoJson Repo(long id, string name, bool archived = false, string pushedAt = "2026-09-30T10:00:00Z") =>
        new(id, name, archived, pushedAt);

    private static string Search(int total, params RepoJson[] items) => JsonSerializer.Serialize(new
    {
        total_count = total,
        incomplete_results = false,
        items = items.Select(r => new
        {
            id = r.Id,
            name = r.Name,
            full_name = $"{Org}/{r.Name}",
            html_url = $"https://github.invalid/{Org}/{r.Name}",
            archived = r.Archived,
            pushed_at = r.PushedAt
        })
    });

    /// <summary>O GitHub devolve o conteúdo em base64 com quebras de linha a cada 60 caracteres.</summary>
    private static string Yaml(string text)
    {
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        var lines = Enumerable.Range(0, (base64.Length + 59) / 60).Select(i => base64.Substring(i * 60, Math.Min(60, base64.Length - i * 60)));
        return JsonSerializer.Serialize(new { type = "file", encoding = "base64", content = string.Join("\n", lines) + "\n" });
    }

    private record RecordedRequest(string Path, string Query, string? Authorization);

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Func<HttpResponseMessage>> _routes = [];
        private string? _searchDefault;
        private readonly Dictionary<int, string> _searchPages = [];
        private (HttpStatusCode Status, (string Remaining, string Reset)? RateLimit)? _searchStatus;

        public List<RecordedRequest> Requests { get; } = [];

        public Exception? FailWith { get; set; }

        public void OnSearch(string json) => _searchDefault = json;

        public void OnSearchPage(int page, string json) => _searchPages[page] = json;

        public void OnSearchStatus(HttpStatusCode status, (string Remaining, string Reset)? rateLimit) =>
            _searchStatus = (status, rateLimit);

        public void OnContents(string repo, string json, string path = "config.yaml") =>
            _routes[$"/repos/{Org}/{repo}/contents/{path}"] = () => Json(json);

        public void OnContentsRaw(string repo, string json) =>
            _routes[$"/repos/{Org}/{repo}/contents/config.yaml"] = () => Json(json);

        public void OnContentsNotFound(string repo) =>
            _routes[$"/repos/{Org}/{repo}/contents/config.yaml"] = () => new HttpResponseMessage(HttpStatusCode.NotFound);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (FailWith is not null)
            {
                throw FailWith;
            }

            var uri = request.RequestUri!;
            Requests.Add(new RecordedRequest(uri.AbsolutePath, uri.Query, request.Headers.Authorization?.ToString()));

            if (uri.AbsolutePath == "/search/repositories")
            {
                if (_searchStatus is { } failure)
                {
                    var failed = new HttpResponseMessage(failure.Status);
                    if (failure.RateLimit is { } limit)
                    {
                        failed.Headers.Add("X-RateLimit-Remaining", limit.Remaining);
                        failed.Headers.Add("X-RateLimit-Reset", limit.Reset);
                    }

                    return Task.FromResult(failed);
                }

                var page = int.Parse(System.Web.HttpUtility.ParseQueryString(uri.Query)["page"] ?? "1");
                var body = _searchPages.TryGetValue(page, out var pageJson) ? pageJson : _searchDefault;
                return Task.FromResult(body is null ? new HttpResponseMessage(HttpStatusCode.NotFound) : Json(body));
            }

            var path = Uri.UnescapeDataString(uri.AbsolutePath);
            return Task.FromResult(_routes.TryGetValue(path, out var route)
                ? route()
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static HttpResponseMessage Json(string body) =>
            new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
