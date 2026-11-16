using CheckApps.Application.Analysis;
using CheckApps.Application.Discovery;
using CheckApps.Domain.Analysis;
using CheckApps.Domain.Discovery;
using CheckApps.Infrastructure.Configuration;
using CheckApps.Infrastructure.DependencyInjection;
using CheckApps.Infrastructure.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Análise por sigla (spec 013): lê os arquivos dos repositórios JÁ CATALOGADOS da sigla e grava os fatos que os scanners
// extraem (versão do .NET, pacotes e, desde a spec 014, as integrações declaradas em appsettings*.json). Nunca cria
// App, nunca lê a organização e nunca guarda o conteúdo dos arquivos nem valores brutos (usuário, senha e query de URL
// são descartados antes de gravar).
//
//   dotnet run --project tools/CheckApps.Analyze -- --sigla ab1,cd2 [--source github|fake] [--dry-run] [--force] [--fake-revision 0|1]
//
// Configuração (nada disso entra no Git): GitHub:Token via `dotnet user-secrets set ... --project tools/CheckApps.Analyze`
// (o armazenamento é o mesmo do CheckApps.GitHubScan) ou variável GitHub__Token. Opcionais: Scan:SiglaPattern,
// Analysis:MaxProjectFiles, Analysis:MaxFileBytes. O token nunca é impresso.

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
        Uso: CheckApps.Analyze --sigla <sigla[,sigla...]> [--source github|fake] [--dry-run] [--force] [--fake-revision <n>]

          --sigla     uma ou mais siglas (obrigatório). Analisa os repositórios JÁ CATALOGADOS dessas siglas.
          --source    github (padrão) lê o GitHub; fake usa dados de demonstração, sem rede.
                      (sem o argumento vale Scan:Source da configuração; o padrão é github)
          --dry-run   lê e mostra o que seria gravado, sem gravar nenhum dado.
          --force     reanalisa mesmo quando o commit já foi analisado (grava uma análise nova; a anterior fica no
                      histórico). Use depois de atualizar o scanner: sem isso, repositórios sem commit novo dão
                      "sem alteração".
          --fake-revision  só com a fonte fake: 0 (padrão) ou 1, as duas versões do contrato de demonstração, para ver
                      as mudanças de contrato de ponta a ponta.

        Repositórios só entram no catálogo pela triagem (menu Repositórios); rode antes o CheckApps.GitHubScan.
        """);
    return parsed.Error is not null ? ExitUsage : ExitOk;
}

var configurationBuilder = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables();

// --source sobrepõe Scan:Source (que, sem o argumento, vem da configuração; o padrão é github).
if (parsed.Source is not null)
{
    configurationBuilder.AddInMemoryCollection(
        new Dictionary<string, string?> { [$"{ScanOptions.SectionName}:{nameof(ScanOptions.Source)}"] = parsed.Source });
}

if (parsed.FakeRevision is { } fakeRevision)
{
    configurationBuilder.AddInMemoryCollection(
        new Dictionary<string, string?> { [$"{ScanOptions.SectionName}:{nameof(ScanOptions.FakeRevision)}"] = fakeRevision.ToString() });
}

// Modo demonstração (spec 023): no modo demo, banco e fontes de demonstração vencem até o --source.
var configuration = configurationBuilder.BuildWithMode();
Console.WriteLine(CheckAppsMode.Describe(configuration));

// O escopo é validado antes de montar qualquer coisa: sigla vazia ou fora do formato é recusada sem pedir token
// nem consultar nada (o AnalysisService valida de novo — a regra é dele, isto é só ordem).
var scanOptions = configuration.GetSection(ScanOptions.SectionName).Get<ScanOptions>() ?? new ScanOptions();
try
{
    ScanScope.Create(parsed.Siglas, scanOptions.SiglaPattern);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"Erro: {ex.Message}");
    return ExitUsage;
}

try
{
    // A composição (fontes, scanners, serviço) é a mesma da API: AddCheckAppsInfrastructure.
    var services = new ServiceCollection();
    services.AddCheckAppsInfrastructure(configuration);
    services.AddSingleton(TimeProvider.System);

    await using var provider = services.BuildServiceProvider();
    await provider.GetRequiredService<SqliteSchemaBootstrapper>().EnsureSchemaAsync();

    using var scope = provider.CreateScope();
    var report = await scope.ServiceProvider.GetRequiredService<AnalysisService>().AnalyzeAsync(parsed.Siglas, parsed.DryRun, parsed.Force);
    Print(report, fake: string.Equals(scanOptions.Source, "fake", StringComparison.OrdinalIgnoreCase));
    return report.Count(AnalysisOutcome.Failed) > 0 ? ExitSource : ExitOk;
}
catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
{
    // Escopo inválido ou configuração faltando: as mensagens são nossas e nunca trazem o token.
    Console.Error.WriteLine($"Erro: {ex.Message}");
    return ExitUsage;
}
catch (Exception ex) when (ex is RepositorySourceException)
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

static void Print(AnalysisReport report, bool fake)
{
    Console.WriteLine(report.DryRun
        ? "Simulação (--dry-run): nada será gravado."
        : "Análise concluída.");
    if (fake)
    {
        Console.WriteLine("Fonte: fake (dados de demonstração, sem rede).");
    }

    Console.WriteLine();

    foreach (var item in report.Items.OrderBy(i => i.Catalog.AppName).ThenBy(i => i.Catalog.Repository.Name))
    {
        var outcome = item.Outcome switch
        {
            AnalysisOutcome.Analyzed => item.Status ?? "analisado",
            AnalysisOutcome.Unchanged => "sem alteração",
            AnalysisOutcome.Failed => "falha",
            _ => "pulado"
        };

        var facts = item.Outcome == AnalysisOutcome.Analyzed
            ? string.Concat((item.FactCounts ?? []).Select(c => $" · {c.Count} {c.Label}"))
            : "";
        Console.WriteLine($"  [{outcome,-13}] {item.Catalog.Repository.Name}  (app {item.Catalog.AppName}){facts}");
        foreach (var warning in item.Warnings)
        {
            Console.WriteLine($"                   - {warning}");
        }
    }

    if (report.Items.Count == 0)
    {
        Console.WriteLine("  Nenhum repositório catalogado nessa sigla. Crie ou vincule os apps pela triagem antes de analisar.");
    }

    Console.WriteLine();
    Console.WriteLine(
        $"{report.Count(AnalysisOutcome.Analyzed)} analisados " +
        $"({report.CountStatus(AnalysisStatuses.Ok)} ok, {report.CountStatus(AnalysisStatuses.Partial)} parciais, " +
        $"{report.CountStatus(AnalysisStatuses.NoScanner)} sem scanner), " +
        $"{report.Count(AnalysisOutcome.Unchanged)} sem alteração, {report.Count(AnalysisOutcome.Failed)} com falha, " +
        $"{report.Count(AnalysisOutcome.Skipped)} pulados.");
    if (report.DryRun)
    {
        Console.WriteLine("Nada foi gravado.");
    }
}

static (string[] Siglas, string? Source, bool DryRun, bool Force, int? FakeRevision, bool Help, string? Error) ParseArguments(string[] args)
{
    var siglas = new List<string>();
    string? source = null;
    var dryRun = false;
    var force = false;
    int? fakeRevision = null;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--help" or "-h":
                return ([], source, dryRun, force, fakeRevision, true, null);
            case "--dry-run":
                dryRun = true;
                break;
            case "--force":
                force = true;
                break;
            case "--sigla":
                if (i + 1 >= args.Length)
                {
                    return ([], source, dryRun, force, fakeRevision, false, "--sigla precisa de um valor (ex.: --sigla ab1,cd2).");
                }

                siglas.AddRange(args[++i].Split(',', StringSplitOptions.TrimEntries));
                break;
            case "--fake-revision":
                if (i + 1 >= args.Length || args[i + 1] is not ("0" or "1"))
                {
                    return ([], source, dryRun, force, fakeRevision, false, "--fake-revision deve ser 0 ou 1.");
                }

                fakeRevision = int.Parse(args[++i]);
                break;
            case "--source":
                if (i + 1 >= args.Length || args[i + 1] is not ("github" or "fake"))
                {
                    return ([], source, dryRun, force, fakeRevision, false, "--source deve ser github ou fake.");
                }

                source = args[++i];
                break;
            default:
                return ([], source, dryRun, force, fakeRevision, false, $"argumento desconhecido: {args[i]}");
        }
    }

    if (fakeRevision is not null && source != "fake")
    {
        return ([], source, dryRun, force, fakeRevision, false, "--fake-revision só vale com --source fake.");
    }

    return (siglas.ToArray(), source, dryRun, force, fakeRevision, false, null);
}
