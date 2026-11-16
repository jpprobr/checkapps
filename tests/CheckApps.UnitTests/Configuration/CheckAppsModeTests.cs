using CheckApps.Application.Discovery;
using CheckApps.Infrastructure.Configuration;
using CheckApps.Infrastructure.Sqlite;
using Microsoft.Extensions.Configuration;

namespace CheckApps.UnitTests.Configuration;

/// <summary>O modo demonstração (spec 023, ADR-013): a camada do modo demo vence a configuração local, e o modo real não muda nada.</summary>
public class CheckAppsModeTests
{
    /// <summary>Configuração "de verdade" de uma máquina: banco real, GitHub e token, como viria de user-secrets e variáveis.</summary>
    private static readonly Dictionary<string, string?> LocalSettings = new()
    {
        ["Sqlite:DatabasePath"] = "checkapps.db",
        ["Scan:Source"] = "github",
        ["Scan:NameTemplate"] = "org-{sigla}-",
        ["Scan:Enabled"] = "false",
        ["GitHub:Token"] = "token-local"
    };

    private static IConfiguration Build(params (string Key, string? Value)[] extra)
    {
        var builder = new ConfigurationBuilder().AddInMemoryCollection(LocalSettings);
        if (extra.Length > 0)
        {
            builder.AddInMemoryCollection(extra.ToDictionary(e => e.Key, e => e.Value));
        }

        return builder.BuildWithMode();
    }

    private static ScanOptions Scan(IConfiguration configuration) => configuration.GetSection(ScanOptions.SectionName).Get<ScanOptions>()!;

    [Fact]
    public void WithoutAMode_NothingChanges()
    {
        var configuration = Build();

        Assert.Equal(CheckAppsMode.Real, CheckAppsMode.Read(configuration));
        Assert.Equal("checkapps.db", configuration["Sqlite:DatabasePath"]);
        Assert.Equal("github", Scan(configuration).Source);
        Assert.Equal("org-{sigla}-", Scan(configuration).NameTemplate);
        Assert.False(Scan(configuration).Enabled);
        Assert.False(CheckAppsMode.CanRecreate(configuration));
    }

    [Fact]
    public void RealModeExplicitly_NothingChanges() =>
        Assert.Equal("github", Scan(Build(("CheckApps:Mode", "Real"))).Source);

    [Fact]
    public void DemoMode_WinsOverTheLocalConfiguration()
    {
        var configuration = Build(("CheckApps:Mode", "DEMO"));

        Assert.True(CheckAppsMode.IsDemo(configuration));
        Assert.Equal(CheckAppsMode.DefaultDemoDatabase, configuration["Sqlite:DatabasePath"]);
        Assert.Equal("fake", Scan(configuration).Source);
        Assert.Equal("fake-{sigla}-", Scan(configuration).NameTemplate);
        Assert.True(Scan(configuration).Enabled);
        Assert.True(CheckAppsMode.CanRecreate(configuration));
    }

    [Fact]
    public void DemoMode_WinsEvenOverAnOverrideAddedLater_LikeTheSourceArgument()
    {
        var builder = new ConfigurationBuilder()
            .AddInMemoryCollection(LocalSettings)
            .AddInMemoryCollection(new Dictionary<string, string?> { ["CheckApps:Mode"] = "demo" })
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Scan:Source"] = "github" });

        Assert.Equal("fake", Scan(builder.BuildWithMode()).Source);
    }

    [Fact]
    public void TheDemoDatabaseName_IsConfigurable()
    {
        var configuration = Build(("CheckApps:Mode", "demo"), ("CheckApps:DemoDatabasePath", "apresentacao.db"));

        Assert.Equal("apresentacao.db", configuration["Sqlite:DatabasePath"]);
        Assert.EndsWith("apresentacao.db", CheckAppsMode.DatabasePath(configuration), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("checkapps.db")]
    [InlineData("CheckApps.DB")]
    [InlineData("C:/dados/checkapps.db")]
    public void TheRealDatabaseName_IsRefusedAsTheDemoDatabase(string name)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(("CheckApps:Mode", "demo"), ("CheckApps:DemoDatabasePath", name)));
        Assert.Contains("banco real", ex.Message);
    }

    [Fact]
    public void AnInvalidMode_IsRefusedWithTheAcceptedValues()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(("CheckApps:Mode", "teste")));

        Assert.Contains("\"real\"", ex.Message);
        Assert.Contains("\"demo\"", ex.Message);
    }

    [Fact]
    public void Describe_SaysTheModeAndTheDatabaseFile()
    {
        Assert.StartsWith("Modo: real", CheckAppsMode.Describe(Build()));

        var demo = CheckAppsMode.Describe(Build(("CheckApps:Mode", "demo")));
        Assert.StartsWith("Modo: demonstração", demo);
        Assert.EndsWith(CheckAppsMode.DefaultDemoDatabase, demo);
    }

    [Fact]
    public void ARelativeDemoDatabase_ResolvesLikeAnyOtherDatabase()
    {
        var configuration = Build(("CheckApps:Mode", "demo"));

        Assert.Equal(new SqliteOptions { DatabasePath = CheckAppsMode.DefaultDemoDatabase }.ResolvePath(), CheckAppsMode.DatabasePath(configuration));
    }

    [Fact]
    public void TheApiConfigurationManager_GetsTheSameLayer()
    {
        var manager = new ConfigurationManager();
        manager.AddInMemoryCollection(LocalSettings);
        manager.AddInMemoryCollection(new Dictionary<string, string?> { ["CheckApps:Mode"] = "demo" });

        manager.ApplyCheckAppsMode();

        Assert.Equal("fake", manager["Scan:Source"]);
        Assert.Equal(CheckAppsMode.DefaultDemoDatabase, manager["Sqlite:DatabasePath"]);
    }
}
