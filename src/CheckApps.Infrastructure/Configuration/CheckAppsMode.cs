using CheckApps.Application.Discovery;
using CheckApps.Infrastructure.Sqlite;
using Microsoft.Extensions.Configuration;

namespace CheckApps.Infrastructure.Configuration;

/// <summary>
/// O modo do CheckApps (spec 023, ADR-013): <c>real</c> (padrão) ou <c>demo</c>, lido de <c>CheckApps:Mode</c> e escolhido na subida.
/// No modo demo, uma camada de configuração entra **por último** e vence <c>user-secrets</c>, variáveis e argumentos: banco de
/// demonstração, fontes <c>fake</c> e scan pela tela ligado. É o único lugar que conhece essa camada, e os quatro pontos de entrada
/// (API, seed, scan e análise) passam por ele, para nunca apontarem para bancos diferentes.
/// </summary>
public static class CheckAppsMode
{
    public const string ModeKey = "CheckApps:Mode";
    public const string DemoDatabaseKey = "CheckApps:DemoDatabasePath";

    public const string Real = "real";
    public const string Demo = "demo";

    public const string DefaultDemoDatabase = "demo.db";

    /// <summary>O nome do banco real: o modo demo nunca o usa nem o apaga.</summary>
    public const string RealDatabase = "checkapps.db";

    /// <summary>O modo configurado, em minúsculas.</summary>
    /// <exception cref="InvalidOperationException">Valor que não é <c>real</c> nem <c>demo</c>.</exception>
    public static string Read(IConfiguration configuration)
    {
        var value = configuration[ModeKey];
        if (string.IsNullOrWhiteSpace(value))
        {
            return Real;
        }

        var mode = value.Trim().ToLowerInvariant();
        return mode is Real or Demo
            ? mode
            : throw new InvalidOperationException($"{ModeKey} deve ser \"{Real}\" ou \"{Demo}\" (valor atual: \"{value}\").");
    }

    public static bool IsDemo(IConfiguration configuration) => Read(configuration) == Demo;

    /// <summary>O que o modo demo sobrepõe. O nome do banco real é recusado, para a demonstração nunca gravar nele.</summary>
    /// <exception cref="InvalidOperationException">O banco de demonstração configurado tem o nome do banco real.</exception>
    public static IReadOnlyDictionary<string, string?> DemoOverrides(IConfiguration configuration)
    {
        var database = string.IsNullOrWhiteSpace(configuration[DemoDatabaseKey]) ? DefaultDemoDatabase : configuration[DemoDatabaseKey]!.Trim();
        if (string.Equals(Path.GetFileName(database), RealDatabase, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{DemoDatabaseKey} não pode ser \"{RealDatabase}\": esse é o banco real.");
        }

        return new Dictionary<string, string?>
        {
            [$"{SqliteOptions.SectionName}:{nameof(SqliteOptions.DatabasePath)}"] = database,
            [$"{ScanOptions.SectionName}:{nameof(ScanOptions.Source)}"] = "fake",
            [$"{ScanOptions.SectionName}:{nameof(ScanOptions.NameTemplate)}"] = "fake-{sigla}-",
            [$"{ScanOptions.SectionName}:{nameof(ScanOptions.Enabled)}"] = "true"
        };
    }

    /// <summary>Para as ferramentas: monta a configuração e, no modo demo, monta de novo com a camada por cima.</summary>
    public static IConfiguration BuildWithMode(this IConfigurationBuilder builder)
    {
        var configuration = builder.Build();
        if (!IsDemo(configuration))
        {
            return configuration;
        }

        builder.AddInMemoryCollection(DemoOverrides(configuration));
        return builder.Build();
    }

    /// <summary>Para a API: o <see cref="ConfigurationManager"/> já lê enquanto é montado; a camada entra por último.</summary>
    public static void ApplyCheckAppsMode(this ConfigurationManager configuration)
    {
        if (IsDemo(configuration))
        {
            configuration.AddInMemoryCollection(DemoOverrides(configuration));
        }
    }

    /// <summary>O caminho completo do banco que esta configuração vai abrir.</summary>
    public static string DatabasePath(IConfiguration configuration) =>
        (configuration.GetSection(SqliteOptions.SectionName).Get<SqliteOptions>() ?? new SqliteOptions()).ResolvePath();

    /// <summary>A linha que as ferramentas imprimem primeiro: em que modo e em que banco vão gravar.</summary>
    public static string Describe(IConfiguration configuration) =>
        $"Modo: {(IsDemo(configuration) ? "demonstração" : "real")} · banco: {DatabasePath(configuration)}";

    /// <summary>
    /// O arquivo pode ser apagado e recriado pelo <c>Seed --demo</c>? Só no modo demo e só se o nome não for o do banco real.
    /// </summary>
    public static bool CanRecreate(IConfiguration configuration) =>
        IsDemo(configuration)
        && !string.Equals(Path.GetFileName(DatabasePath(configuration)), RealDatabase, StringComparison.OrdinalIgnoreCase);
}
