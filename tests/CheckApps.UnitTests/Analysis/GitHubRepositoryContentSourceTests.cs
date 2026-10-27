using System.Net;
using System.Text;
using System.Text.Json;
using CheckApps.Application.Discovery;
using CheckApps.Domain;
using CheckApps.Infrastructure.Analysis;
using CheckApps.Infrastructure.Discovery;

namespace CheckApps.UnitTests.Analysis;

/// <summary>Nenhum teste usa rede: o <see cref="HttpMessageHandler"/> responde com JSON fixo no formato da API do GitHub.</summary>
public class GitHubRepositoryContentSourceTests
{
    // Valor reconhecível, só para provar que ele nunca aparece em nenhuma mensagem.
    private const string Token = "token-falso-SEGREDO-DE-TESTE-123";
    private const string FullName = "lab-org/lab-ab1-app-x";

    private readonly StubHandler _handler = new();

    private GitHubRepositoryContentSource Sut(string? token = Token) =>
        new(new HttpClient(_handler), new GitHubOptions { Organization = "lab-org", Token = token });

    private static Repository Repo(string? provider = "github", string? externalId = "42") =>
        new() { Id = "r1", AppId = "app-1", Name = "lab-ab1-app-x", Provider = provider, ExternalId = externalId };

    private void HappyPath(string branch = "main", string sha = "abc123", bool truncated = false)
    {
        _handler.On("/repositories/42", Json(new { full_name = FullName, default_branch = branch }));
        _handler.On($"/repos/{FullName}/commits/{branch}", Json(new { sha }));
        _handler.On($"/repos/{FullName}/git/trees/{sha}", Json(new
        {
            sha = "tree-sha",
            truncated,
            tree = new object[]
            {
                new { path = "src", type = "tree" },
                new { path = "src/App/App.csproj", type = "blob", size = 120 },
                new { path = "README.md", type = "blob", size = 15 },
                new { path = "vendor/sub", type = "commit" },
            }
        }));
    }

    // ---- Listar -----------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task List_ResolvesTheDefaultBranchCommit_AndKeepsOnlyTheFiles()
    {
        HappyPath();

        var tree = await Sut().ListAsync(Repo());

        Assert.Equal("abc123", tree.CommitSha);
        Assert.False(tree.Truncated);
        Assert.Equal(
            [("src/App/App.csproj", 120L), ("README.md", 15L)],
            tree.Files.Select(f => (f.Path, f.Size)));   // pastas e submódulos ficam de fora

        Assert.Equal(
            ["/repositories/42", $"/repos/{FullName}/commits/main", $"/repos/{FullName}/git/trees/abc123"],
            _handler.Requests.Select(r => r.Path));
        Assert.Contains("recursive=1", _handler.Requests[^1].Query);
        Assert.All(_handler.Requests, r => Assert.Equal($"Bearer {Token}", r.Authorization));
    }

    [Fact]
    public async Task List_ReportsATruncatedTree()
    {
        HappyPath(truncated: true);

        Assert.True((await Sut().ListAsync(Repo())).Truncated);
    }

    [Fact]
    public async Task List_EscapesABranchNameWithASlash()
    {
        HappyPath(branch: "release/1 beta");

        await Sut().ListAsync(Repo());

        Assert.Contains(_handler.Requests, r => r.RawPath == $"/repos/{FullName}/commits/release/1%20beta");
    }

    [Theory]
    [InlineData(401, null, "recusou o token")]
    [InlineData(403, "reset", "limite de chamadas")]
    [InlineData(429, "reset", "limite de chamadas")]
    [InlineData(403, null, "negou o acesso")]
    [InlineData(500, null, "erro (500)")]
    public async Task List_MapsHttpErrorsToFriendlyMessages_ThatNeverContainTheToken(int status, string? rateLimit, string expected)
    {
        _handler.On("/repositories/42", Status((HttpStatusCode)status, rateLimit is null ? null : ("0", "1893456000")));

        var error = await Assert.ThrowsAsync<RepositorySourceException>(() => Sut().ListAsync(Repo()));

        Assert.Contains(expected, error.Message);
        Assert.DoesNotContain(Token, error.Message);
        Assert.DoesNotContain(Token, error.ToString());
    }

    [Fact]
    public async Task List_ARepositoryTheTokenCannotSee_SaysSo()
    {
        _handler.On("/repositories/42", Status(HttpStatusCode.NotFound));

        var error = await Assert.ThrowsAsync<RepositorySourceException>(() => Sut().ListAsync(Repo()));

        Assert.Contains("não foi encontrado no GitHub", error.Message);
        Assert.DoesNotContain(Token, error.Message);
    }

    [Fact]
    public async Task List_AnEmptyRepository_SaysItIsEmpty()
    {
        _handler.On("/repositories/42", Json(new { full_name = FullName, default_branch = "main" }));
        _handler.On($"/repos/{FullName}/commits/main", Status(HttpStatusCode.Conflict));

        var error = await Assert.ThrowsAsync<RepositorySourceException>(() => Sut().ListAsync(Repo()));

        Assert.Contains("vazio", error.Message);
    }

    [Fact]
    public async Task List_NetworkFailure_IsFriendly_AndDoesNotLeakTheToken()
    {
        _handler.FailWith = new HttpRequestException("falha de conexão simulada");

        var error = await Assert.ThrowsAsync<RepositorySourceException>(() => Sut().ListAsync(Repo()));

        Assert.Contains("Não foi possível falar com o GitHub", error.Message);
        Assert.DoesNotContain(Token, error.ToString());
    }

    // ---- Ler um arquivo ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Read_DecodesTheBase64Content_AtTheGivenCommit_EscapingTheSegmentsOfThePath()
    {
        HappyPath();
        const string text = "<Project>ação — acentuação</Project>";
        _handler.OnPath($"/repos/{FullName}/contents/src/Meu App/App.csproj", Json(new { type = "file", encoding = "base64", content = Base64(text) }));
        var sut = Sut();
        await sut.ListAsync(Repo());

        var read = await sut.ReadFileAsync(Repo(), "abc123", "src/Meu App/App.csproj");

        Assert.Equal(text, read);
        var request = _handler.Requests[^1];
        Assert.Equal($"/repos/{FullName}/contents/src/Meu%20App/App.csproj", request.RawPath);
        Assert.Contains("ref=abc123", request.Query);
    }

    [Fact]
    public async Task Read_AMissingFile_IsNull_NotAnError()
    {
        HappyPath();
        var sut = Sut();
        await sut.ListAsync(Repo());

        Assert.Null(await sut.ReadFileAsync(Repo(), "abc123", "nao/existe.csproj"));
    }

    [Theory]
    [InlineData("""[{"name":"a"}]""")]                          // um diretório volta como lista
    [InlineData("""{"type":"file","encoding":"none"}""")]       // arquivo grande volta sem conteúdo
    [InlineData("""{"type":"file","content":"@@não-é-base64@@"}""")]
    public async Task Read_ContentThatCannotBeDecoded_IsNull(string body)
    {
        HappyPath();
        _handler.OnPath($"/repos/{FullName}/contents/x.csproj", RawJson(body));
        var sut = Sut();
        await sut.ListAsync(Repo());

        Assert.Null(await sut.ReadFileAsync(Repo(), "abc123", "x.csproj"));
    }

    [Fact]
    public async Task Read_WithoutAPreviousList_ResolvesTheFullNameOnce()
    {
        HappyPath();
        _handler.OnPath($"/repos/{FullName}/contents/a.csproj", Json(new { content = Base64("a") }));
        _handler.OnPath($"/repos/{FullName}/contents/b.csproj", Json(new { content = Base64("b") }));
        var sut = Sut();

        Assert.Equal("a", await sut.ReadFileAsync(Repo(), "abc123", "a.csproj"));
        Assert.Equal("b", await sut.ReadFileAsync(Repo(), "abc123", "b.csproj"));

        Assert.Single(_handler.Requests, r => r.Path == "/repositories/42");
    }

    // ---- Configuração e escopo ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("github", "42", true)]
    [InlineData("GitHub", "42", true)]
    [InlineData("fake", "42", false)]
    [InlineData(null, "42", false)]
    [InlineData("github", "abc", false)]
    [InlineData("github", "-1", false)]
    [InlineData("github", null, false)]
    public void CanRead_OnlyGitHubRepositoriesWithANumericExternalId(string? provider, string? externalId, bool expected) =>
        Assert.Equal(expected, Sut().CanRead(Repo(provider, externalId)));

    [Fact]
    public async Task WithoutAToken_ItExplainsWhatToConfigure_NeverTouchesTheNetwork_AndSendsNoAuthorization()
    {
        var sut = Sut(token: null);

        Assert.Contains("GitHub:Token", sut.ConfigurationProblem);
        Assert.Contains("user-secrets", sut.ConfigurationProblem);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ListAsync(Repo()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ReadFileAsync(Repo(), "abc123", "a.csproj"));
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public void WithAToken_ThereIsNothingToConfigure_AndTheMessageNeverHasTheValue()
    {
        Assert.Null(Sut().ConfigurationProblem);
        Assert.DoesNotContain(Token, Sut(token: " ").ConfigurationProblem);
    }

    // ---- Apoio -------------------------------------------------------------------------------------------------------------

    private static string Base64(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

    private static Func<HttpResponseMessage> Json(object body) => RawJson(JsonSerializer.Serialize(body));

    private static Func<HttpResponseMessage> RawJson(string body) =>
        () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static Func<HttpResponseMessage> Status(HttpStatusCode status, (string Remaining, string Reset)? rateLimit = null) =>
        () =>
        {
            var response = new HttpResponseMessage(status);
            if (rateLimit is { } limit)
            {
                response.Headers.Add("X-RateLimit-Remaining", limit.Remaining);
                response.Headers.Add("X-RateLimit-Reset", limit.Reset);
            }

            return response;
        };

    private record RecordedRequest(string Path, string RawPath, string Query, string? Authorization);

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Func<HttpResponseMessage>> _routes = [];

        public List<RecordedRequest> Requests { get; } = [];

        public Exception? FailWith { get; set; }

        /// <summary>Rota pelo caminho já decodificado; vale para qualquer query string.</summary>
        public void On(string path, Func<HttpResponseMessage> response) => _routes[path] = response;

        public void OnPath(string path, Func<HttpResponseMessage> response) => _routes[path] = response;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (FailWith is not null)
            {
                throw FailWith;
            }

            var uri = request.RequestUri!;
            Requests.Add(new RecordedRequest(
                Uri.UnescapeDataString(uri.AbsolutePath), uri.AbsolutePath, uri.Query, request.Headers.Authorization?.ToString()));

            var path = Uri.UnescapeDataString(uri.AbsolutePath);
            return Task.FromResult(_routes.TryGetValue(path, out var route)
                ? route()
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
