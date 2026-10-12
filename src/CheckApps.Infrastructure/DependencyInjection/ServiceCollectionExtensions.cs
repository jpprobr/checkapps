using CheckApps.Application.Persistence;
using CheckApps.Application.Sonar;
using CheckApps.Domain.QualityAssessment;
using CheckApps.Infrastructure.Sonar;
using CheckApps.Infrastructure.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CheckApps.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCheckAppsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SqliteOptions>(configuration.GetSection(SqliteOptions.SectionName));

        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<SqliteSchemaBootstrapper>();

        services.AddSingleton<ISquadRepository, SquadRepository>();
        services.AddSingleton<IAppRepository, AppRepository>();
        services.AddSingleton<IQualitySnapshotRepository, QualitySnapshotRepository>();
        services.AddSingleton<ICheckRepository, CheckRepository>();

        services.AddSingleton<IQualityAssessmentEngine, QualityAssessmentEngineV1>();
        services.AddSingleton<ISonarProvider, MockSonarProvider>();

        return services;
    }
}
