using CheckApps.Application.Discovery;
using CheckApps.Application.Persistence;
using CheckApps.Domain.Discovery;
using CheckApps.Infrastructure.DependencyInjection;
using CheckApps.Infrastructure.Discovery;
using CheckApps.Infrastructure.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Scan por sigla (spec 005): descobre repositórios e os grava na caixa de entrada. Nunca cria App.
//
//   dotnet run --project tools/CheckApps.GitHubScan -- --sigla ab1,cd2 [--source github|fake] [--dry-run]
//
// Configuração (nada disso entra no Git): GitHub:Organization e GitHub:Token via
// `dotnet user-secrets set ... --project tools/CheckApps.GitHubScan` ou variáveis GitHub__Organization e
// GitHub__Token; Scan:NameTemplate (ex.: {prefixo}-{sigla}-), Scan:SiglaPattern, Scan:MaxResults,
// GitHub:ConfigFilePath e GitHub:SiglaAppKey (opcionais). O token nunca é impresso.

const int ExitOk = 0, ExitUnexpected = 1, ExitUsage = 2, ExitSource = 3;

var parsed = ParseArguments(args);
if (parsed.Error is not null || parsed.Help)
{
    if (parsed.Error is not null)
    {
        Console.Error.WriteLine($"Erro: {parsed.Error}");
        Console.Error.WriteLine();
    }

    Console.WriteLine("""
        Uso: CheckApps.GitHubScan --sigla <sigla[,sigla...]> [--source github|fake] [--dry-run]

          --sigla     uma ou mais siglas (obrigatório). O scan nunca lê a organização inteira.
          --source    github (padrão) lê o GitHub; fake usa dados de demonstração, sem rede.
          --dry-run   lê e mostra o que seria gravado, sem gravar nenhum dado.
        """);
    return parsed.Error is not null ? ExitUsage : ExitOk;
}

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

var useFake = parsed.Source == "fake";

var services = new ServiceCollection();
services.AddCheckAppsInfrastructure(configuration);
services.AddSingleton(TimeProvider.System);

// Sem convenção configurada, a demonstração usa uma fictícia — para a fonte real ela é obrigatória.
var scanOptions = configuration.GetSection(ScanOptions.SectionName).Get<ScanOptions>() ?? new ScanOptions();
if (useFake && string.IsNullOrWhiteSpace(scanOptions.NameTemplate))
{
    scanOptions.NameTemplate = "fake-{sigla}-";
}

// O escopo é validado antes de montar a fonte: sigla vazia ou fora do formato é recusada sem
// pedir token nem consultar nada (o ScanService valida de novo — a regra é dele, isto é só ordem).
try
{
    ScanScope.Create(parsed.Siglas, scanOptions.SiglaPattern);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"Erro: {ex.Message}");
    return ExitUsage;
}

services.AddSingleton(scanOptions);

if (useFake)
{
    services.AddSingleton<IRepositoryDiscoverySource, FakeRepositorySource>();
}
else
{
    var gitHubOptions = configuration.GetSection(GitHubOptions.SectionName).Get<GitHubOptions>() ?? new GitHubOptions();
    services.AddSingleton(gitHubOptions);
    services.AddHttpClient<GitHubRepositorySource>();
    services.AddSingleton<IRepositoryDiscoverySource>(sp => sp.GetRequiredService<GitHubRepositorySource>());
}

services.AddSingleton<ScanService>();

try
{
    await using var provider = services.BuildServiceProvider();
    await provider.GetRequiredService<SqliteSchemaBootstrapper>().EnsureSchemaAsync();

    var report = await provider.GetRequiredService<ScanService>().ScanAsync(parsed.Siglas, parsed.DryRun);
    Print(report, useFake);
    return ExitOk;
}
catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
{
    // Escopo inválido ou configuração faltando: as mensagens são nossas e nunca trazem o token.
    Console.Error.WriteLine($"Erro: {ex.Message}");
    return ExitUsage;
}
catch (Exception ex) when (ex is RepositorySourceException or ScanLimitExceededException)
{
    Console.Error.WriteLine($"Erro: {ex.Message}");
    return ExitSource;
}
catch (Exception ex)
{
    // Só o tipo e a mensagem — nunca o stack trace completo, que poderia expor detalhes da configuração.
    Console.Error.WriteLine($"Erro inesperado ({ex.GetType().Name}): {ex.Message}");
    return ExitUnexpected;
}

static void Print(ScanReport report, bool fake)
{
    Console.WriteLine(report.DryRun
        ? "Simulação (--dry-run): nada será gravado."
        : "Scan concluído.");
    if (fake)
    {
        Console.WriteLine("Fonte: fake (dados de demonstração, sem rede).");
    }

    Console.WriteLine();

    foreach (var item in report.Items.OrderBy(i => i.Repository.Sigla).ThenBy(i => i.Repository.Name))
    {
        var r = item.Repository;
        var outcome = item.Outcome switch
        {
            UpsertOutcome.New => "novo",
            UpsertOutcome.Updated => "atualizado",
            _ => "inalterado"
        };

        var role = item.Suggestion.Role ?? "papel não identificado";
        var confidence = item.Suggestion.Confidence == SuggestionConfidence.High ? "alta" : "baixa";
        var flags = r.Archived ? "  [arquivado]" : "";
        Console.WriteLine($"  [{outcome,-10}] {r.Name}{flags}");
        Console.WriteLine($"                 sigla-app: {r.SiglaApp ?? "—"} · sugestão: {role} (confiança {confidence}) · último push: {r.PushedAt?.ToString("yyyy-MM-dd") ?? "—"}");
    }

    if (report.Items.Count == 0)
    {
        Console.WriteLine("  Nenhum repositório encontrado dentro da convenção de nome.");
    }

    if (report.Warnings.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Avisos:");
        foreach (var warning in report.Warnings)
        {
            Console.WriteLine($"  - {warning}");
        }
    }

    Console.WriteLine();
    Console.WriteLine(
        $"{report.Count(UpsertOutcome.New)} novos, {report.Count(UpsertOutcome.Updated)} atualizados, " +
        $"{report.Count(UpsertOutcome.Unchanged)} inalterados.");
    if (report.DryRun)
    {
        Console.WriteLine("Nada foi gravado.");
    }
}

static (string[] Siglas, string Source, bool DryRun, bool Help, string? Error) ParseArguments(string[] args)
{
    var siglas = new List<string>();
    var source = "github";
    var dryRun = false;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--help" or "-h":
                return ([], source, dryRun, true, null);
            case "--dry-run":
                dryRun = true;
                break;
            case "--sigla":
                if (i + 1 >= args.Length)
                {
                    return ([], source, dryRun, false, "--sigla precisa de um valor (ex.: --sigla ab1,cd2).");
                }

                siglas.AddRange(args[++i].Split(',', StringSplitOptions.TrimEntries));
                break;
            case "--source":
                if (i + 1 >= args.Length || args[i + 1] is not ("github" or "fake"))
                {
                    return ([], source, dryRun, false, "--source deve ser github ou fake.");
                }

                source = args[++i];
                break;
            default:
                return ([], source, dryRun, false, $"argumento desconhecido: {args[i]}");
        }
    }

    return (siglas.ToArray(), source, dryRun, false, null);
}
