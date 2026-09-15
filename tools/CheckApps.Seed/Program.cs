using CheckApps.Application.Persistence;
using CheckApps.Domain;
using CheckApps.Domain.QualityAssessment;
using CheckApps.Infrastructure.DependencyInjection;
using CheckApps.Infrastructure.DynamoDb;
using CheckApps.Infrastructure.Seeding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection();
services.AddCheckAppsInfrastructure(configuration);
await using var provider = services.BuildServiceProvider();

await provider.GetRequiredService<DynamoDbTableBootstrapper>().EnsureTableExistsAsync();

var engine = provider.GetRequiredService<IQualityAssessmentEngine>();
var demoData = new DemoDataFactory(engine).Build();

var squadRepository = provider.GetRequiredService<ISquadRepository>();
var appRepository = provider.GetRequiredService<IAppRepository>();
var snapshotRepository = provider.GetRequiredService<IQualitySnapshotRepository>();

Console.WriteLine($"Seedando {demoData.Squads.Count} squads e {demoData.Apps.Count} apps...");

foreach (var squad in demoData.Squads)
{
    await squadRepository.SaveAsync(squad);
}

foreach (var app in demoData.Apps)
{
    // Grava só os dados base do App: o estado atual (currentQualityLevel etc.) é
    // reconstruído abaixo ao repetir o histórico de snapshots pelo mesmo caminho
    // transacional usado em produção (QualitySnapshotRepository.AddAsync).
    await appRepository.SaveAsync(new App
    {
        Id = app.Id,
        SquadId = app.SquadId,
        Name = app.Name,
        Description = app.Description,
        Criticality = app.Criticality,
        Tags = app.Tags,
        Links = app.Links,
        Status = app.Status,
        ExternalId = app.ExternalId
    });
}

foreach (var repository in demoData.Repositories)
{
    await appRepository.AddRepositoryAsync(repository);
}

foreach (var sonarProject in demoData.SonarProjects)
{
    await appRepository.AddSonarProjectAsync(sonarProject);
}

foreach (var appSnapshots in demoData.QualitySnapshots.GroupBy(s => s.AppId))
{
    foreach (var snapshot in appSnapshots.OrderBy(s => s.CollectedAt))
    {
        await snapshotRepository.AddAsync(snapshot);
    }
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
