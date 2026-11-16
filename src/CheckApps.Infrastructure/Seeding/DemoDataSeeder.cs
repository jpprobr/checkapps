using CheckApps.Application.Persistence;
using CheckApps.Domain;

namespace CheckApps.Infrastructure.Seeding;

/// <summary>
/// Grava a massa de demonstração básica (squads, apps, repositórios, projetos Sonar e histórico de qualidade). Usada pelo seed comum e
/// pela base de demonstração completa (spec 023). Idempotente: as gravações sobrescrevem pelo id.
/// </summary>
public static class DemoDataSeeder
{
    public static async Task SeedAsync(
        DemoDataSet demoData, ISquadRepository squads, IAppRepository apps, IQualitySnapshotRepository snapshots,
        CancellationToken cancellationToken = default)
    {
        foreach (var squad in demoData.Squads)
        {
            await squads.SaveAsync(squad, cancellationToken);
        }

        foreach (var app in demoData.Apps)
        {
            // Grava só os dados base do App: o estado de qualidade atual é derivado do snapshot mais recente na leitura
            // (ADR-009), não armazenado.
            await apps.SaveAsync(new App
            {
                Id = app.Id,
                SquadId = app.SquadId,
                Name = app.Name,
                Description = app.Description,
                Criticality = app.Criticality,
                Tags = app.Tags,
                Status = app.Status,
                ExternalId = app.ExternalId
            }, cancellationToken);
        }

        foreach (var repository in demoData.Repositories)
        {
            await apps.AddRepositoryAsync(repository, cancellationToken);
        }

        foreach (var sonarProject in demoData.SonarProjects)
        {
            await apps.AddSonarProjectAsync(sonarProject, cancellationToken);
        }

        foreach (var appSnapshots in demoData.QualitySnapshots.GroupBy(s => s.AppId))
        {
            foreach (var snapshot in appSnapshots.OrderBy(s => s.CollectedAt))
            {
                await snapshots.AddAsync(snapshot, cancellationToken);
            }
        }
    }
}
