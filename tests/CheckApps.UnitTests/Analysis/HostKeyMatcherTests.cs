using CheckApps.Application.Analysis;
using CheckApps.Domain.Analysis;

namespace CheckApps.UnitTests.Analysis;

/// <summary>Spec 017: validação da chave de host e casamento do modelo <c>{env}</c> com o host concreto.</summary>
public class HostKeyMatcherTests
{
    private static readonly string[] Env = ["dev", "hom", "prod"];

    private static readonly DateTimeOffset At = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    // ---- Normalize -------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("empresa.exemplo.com", "empresa.exemplo.com")]
    [InlineData("  Empresa.Exemplo.COM  ", "empresa.exemplo.com")]
    [InlineData("empresa.exemplo.com:8080", "empresa.exemplo.com:8080")]
    [InlineData("empresa.{env}.exemplo.com", "empresa.{env}.exemplo.com")]
    [InlineData("EMPRESA.{ENV}.exemplo.com:443", "empresa.{env}.exemplo.com:443")]
    [InlineData("{env}.exemplo.com", "{env}.exemplo.com")]
    [InlineData("servico_interno", "servico_interno")]
    public void Normalize_AcceptsAHostWithOptionalPort_AndTheEnvPlaceholder(string input, string expected) =>
        Assert.Equal(expected, HostKeyMatcher.Normalize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://empresa.exemplo.com")]
    [InlineData("empresa.exemplo.com/pagamentos")]
    [InlineData("empresa exemplo.com")]
    [InlineData("user@empresa.exemplo.com")]
    [InlineData("empresa.exemplo.com?x=1")]
    [InlineData("empresa.{outro}.exemplo.com")]
    [InlineData("empresa.{env.exemplo.com")]
    [InlineData("empresa.exemplo.com:abc")]
    [InlineData("empresa.exemplo.com:0")]
    [InlineData("empresa.exemplo.com:70000")]
    [InlineData(".exemplo.com")]
    [InlineData("exemplo.com.")]
    public void Normalize_RejectsEverythingThatIsNotAHostKey(string? input) =>
        Assert.Throws<ArgumentException>(() => HostKeyMatcher.Normalize(input));

    // ---- Cover -----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Cover_SameHost_IsExact_IgnoringCase() =>
        Assert.Equal(HostKeyMatcher.ViaExact, HostKeyMatcher.Cover("empresa.dev.exemplo.com", "EMPRESA.dev.exemplo.com", Env));

    [Theory]
    [InlineData("empresa.dev.exemplo.com")]
    [InlineData("empresa.hom.exemplo.com")]
    [InlineData("empresa.prod.exemplo.com")]
    [InlineData("EMPRESA.PROD.exemplo.com")]
    public void Cover_TemplateCoversEachConfiguredEnvironment(string host) =>
        Assert.Equal(HostKeyMatcher.ViaTemplate, HostKeyMatcher.Cover("empresa.{env}.exemplo.com", host, Env));

    [Theory]
    [InlineData("empresa.qa.exemplo.com")]          // valor fora da lista: nunca por palpite
    [InlineData("empresa.homolog.exemplo.com")]
    [InlineData("outra.dev.exemplo.com")]
    [InlineData("empresa.dev.exemplo.com.br")]
    [InlineData("empresa..exemplo.com")]
    public void Cover_TemplateDoesNotCoverOtherHosts(string host) =>
        Assert.Null(HostKeyMatcher.Cover("empresa.{env}.exemplo.com", host, Env));

    [Fact]
    public void Cover_ConfiguredValues_ReplaceTheDefaults() =>
        Assert.Equal(HostKeyMatcher.ViaTemplate, HostKeyMatcher.Cover("empresa.{env}.exemplo.com", "empresa.qa.exemplo.com", ["qa"]));

    [Fact]
    public void Cover_PortIsPartOfTheKey()
    {
        Assert.Equal(HostKeyMatcher.ViaTemplate, HostKeyMatcher.Cover("a.{env}.x.com:8080", "a.dev.x.com:8080", Env));
        Assert.Null(HostKeyMatcher.Cover("a.{env}.x.com:8080", "a.dev.x.com", Env));
        Assert.Null(HostKeyMatcher.Cover("a.{env}.x.com", "a.dev.x.com:8080", Env));
    }

    [Fact]
    public void Cover_AHostThatIsItselfATemplate_MatchesOnlyByEquality()
    {
        Assert.Equal(HostKeyMatcher.ViaExact, HostKeyMatcher.Cover("a.{env}.x.com", "a.{env}.x.com", Env));
        Assert.Null(HostKeyMatcher.Cover("a.dev.x.com", "a.{env}.x.com", Env));
        Assert.Null(HostKeyMatcher.Cover("{env}.x.com", "a.{env}.x.com", Env));
    }

    // ---- Resolver --------------------------------------------------------------------------------------------------------

    private static HostLink Link(string host, string app, string state) =>
        new() { Id = $"{host}|{app}", HostKey = host, AppId = app, State = state, DecidedAt = At };

    private static HostLinkResolver Resolver(params HostLink[] links) =>
        new(links, new Dictionary<string, string> { ["pag"] = "Pagamentos", ["fat"] = "Faturas", ["est"] = "Estoque" }, Env);

    [Fact]
    public void Resolve_WithoutLinks_IsNone()
    {
        var info = Resolver().Resolve("empresa.dev.exemplo.com");

        Assert.Null(info.Confirmed);
        Assert.Empty(info.Rejected);
        Assert.Equal(LinkStatuses.None, info.Status);
    }

    [Fact]
    public void Resolve_TemplateLink_CoversTheConcreteHost_AndSaysItCameFromTheTemplate()
    {
        var info = Resolver(Link("empresa.{env}.exemplo.com", "pag", HostLinkStates.Confirmed)).Resolve("empresa.hom.exemplo.com");

        Assert.NotNull(info.Confirmed);
        Assert.Equal("pag", info.Confirmed.AppId);
        Assert.Equal("Pagamentos", info.Confirmed.AppName);
        Assert.Equal(HostKeyMatcher.ViaTemplate, info.Confirmed.Via);
        Assert.Equal("empresa.{env}.exemplo.com", info.Confirmed.Template);
        Assert.Equal(LinkStatuses.Confirmed, info.Status);
    }

    [Fact]
    public void Resolve_ExactConfirmedWinsOverTheTemplate()
    {
        var info = Resolver(
            Link("empresa.{env}.exemplo.com", "pag", HostLinkStates.Confirmed),
            Link("empresa.dev.exemplo.com", "fat", HostLinkStates.Confirmed)).Resolve("empresa.dev.exemplo.com");

        Assert.Equal("fat", info.Confirmed!.AppId);
        Assert.Equal(HostKeyMatcher.ViaExact, info.Confirmed.Via);
        Assert.Null(info.Confirmed.Template);
    }

    [Fact]
    public void Resolve_OnlyRejections_AreListedByAppName_AndTheStatusIsRejected()
    {
        var info = Resolver(
            Link("gateway.exemplo.com", "pag", HostLinkStates.Rejected),
            Link("gateway.exemplo.com", "est", HostLinkStates.Rejected)).Resolve("gateway.exemplo.com");

        Assert.Null(info.Confirmed);
        Assert.Equal(["Estoque", "Pagamentos"], info.Rejected.Select(r => r.AppName));
        Assert.Equal(LinkStatuses.Rejected, info.Status);
    }

    [Fact]
    public void Resolve_ARejectionOfTheConfirmedApp_IsNotShown()
    {
        var info = Resolver(
            Link("empresa.{env}.exemplo.com", "pag", HostLinkStates.Confirmed),
            Link("empresa.dev.exemplo.com", "pag", HostLinkStates.Rejected),
            Link("empresa.dev.exemplo.com", "fat", HostLinkStates.Rejected)).Resolve("empresa.dev.exemplo.com");

        Assert.Equal("pag", info.Confirmed!.AppId);
        Assert.Equal(["fat"], info.Rejected.Select(r => r.AppId));
    }

    [Fact]
    public void Resolve_UnknownApp_FallsBackToTheId()
    {
        var info = new HostLinkResolver(
            [Link("a.exemplo.com", "app-removido", HostLinkStates.Confirmed)], new Dictionary<string, string>(), Env)
            .Resolve("a.exemplo.com");

        Assert.Equal("app-removido", info.Confirmed!.AppName);
    }
}
