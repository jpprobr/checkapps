using CheckApps.Application.DependencyInjection;
using CheckApps.Application.Persistence;
using CheckApps.Domain.QualityAssessment;
using CheckApps.Infrastructure.Configuration;
using CheckApps.Infrastructure.DependencyInjection;
using CheckApps.Infrastructure.Seeding;
using CheckApps.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// Seed da massa de demonstração.
//
//   dotnet run --project tools/CheckApps.Seed              # massa básica no banco do modo atual (idempotente)
//   dotnet run --project tools/CheckApps.Seed -- --demo    # base de demonstração COMPLETA, recriada do zero e sem rede (spec 023)
//
// O --demo liga o modo demonstração sozinho (CheckApps:Mode=demo): o banco é o de demonstração (padrão demo.db) e as fontes são as
// fake, mesmo com token configurado. Ele só apaga o arquivo do banco de demonstração; o banco real nunca é tocado.

var demo = args.Contains("--demo");
var unknown = args.Where(a => a != "--demo").ToList();
if (unknown.Count > 0)
{
    Console.Error.WriteLine($"Argumento desconhecido: {string.Join(' ', unknown)}. Uso: CheckApps.Seed [--demo]");
    return 2;
}

var configurationBuilder = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables();

if (demo)
{
    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?> { [CheckAppsMode.ModeKey] = CheckAppsMode.Demo });
}

IConfiguration configuration;
try
{
    configuration = configurationBuilder.BuildWithMode();
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine($"Erro: {ex.Message}");
    return 2;
}

Console.WriteLine(CheckAppsMode.Describe(configuration));

if (demo)
{
    if (!CheckAppsMode.CanRecreate(configuration))
    {
        Console.Error.WriteLine("Erro: o --demo só recria o banco de demonstração, e este não é um.");
        return 2;
    }

    // Recria do zero: a base de demonstração é descartável e reproduzível.
    var path = CheckAppsMode.DatabasePath(configuration);
    SqliteConnection.ClearAllPools();
    foreach (var file in new[] { path, $"{path}-wal", $"{path}-shm" })
    {
        File.Delete(file);
    }
}

var services = new ServiceCollection();
services.AddCheckAppsInfrastructure(configuration);
services.AddCheckAppsApplication();
await using var provider = services.BuildServiceProvider();

await provider.GetRequiredService<SqliteSchemaBootstrapper>().EnsureSchemaAsync();

var demoData = new DemoDataFactory(provider.GetRequiredService<IQualityAssessmentEngine>()).Build();
var squadRepository = provider.GetRequiredService<ISquadRepository>();
var appRepository = provider.GetRequiredService<IAppRepository>();

Console.WriteLine($"Seedando {demoData.Squads.Count} squads e {demoData.Apps.Count} apps...");
await DemoDataSeeder.SeedAsync(demoData, squadRepository, appRepository, provider.GetRequiredService<IQualitySnapshotRepository>());

if (demo)
{
    Console.WriteLine("Montando a base de demonstração (descoberta, triagem, contrato em dois commits, jornadas e checagem)...");
    using var scope = provider.CreateScope();
    var report = await ActivatorUtilities.CreateInstance<DemoEnvironmentBuilder>(scope.ServiceProvider).BuildAsync();
    Console.WriteLine($"  apps criados pela triagem: {string.Join(", ", report.CreatedApps)}");
    Console.WriteLine($"  jornadas: {report.Journeys} · checagem: {report.Check}");
}

Console.WriteLine("Seed concluído. Resumo:");
foreach (var squad in await squadRepository.ListAsync())
{
    var squadApps = await appRepository.ListBySquadAsync(squad.Id);
    Console.WriteLine($"- {squad.Name} ({squad.Id}): {squadApps.Count} apps");
    foreach (var app in squadApps)
    {
        Console.WriteLine($"    {app.Name} -> {app.CurrentQualityLevel} (último snapshot: {app.LastSnapshotAt:yyyy-MM-dd})");
    }
}

if (demo)
{
    Console.WriteLine();
    Console.WriteLine("Para usar: suba a API com CheckApps__Mode=demo (a tela mostra a faixa \"Modo demonstração\").");
}

return 0;
