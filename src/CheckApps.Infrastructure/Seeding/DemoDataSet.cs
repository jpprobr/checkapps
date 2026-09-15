using CheckApps.Domain;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.Infrastructure.Seeding;

public record DemoDataSet(
    IReadOnlyList<Squad> Squads,
    IReadOnlyList<App> Apps,
    IReadOnlyList<Repository> Repositories,
    IReadOnlyList<SonarProject> SonarProjects,
    IReadOnlyList<QualitySnapshot> QualitySnapshots);
