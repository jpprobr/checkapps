using CheckApps.Application.Apps;
using CheckApps.Application.QualitySnapshots;
using CheckApps.Application.Squads;
using Microsoft.Extensions.DependencyInjection;

namespace CheckApps.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCheckAppsApplication(this IServiceCollection services)
    {
        services.AddScoped<SquadService>();
        services.AddScoped<AppService>();
        services.AddScoped<QualitySnapshotService>();
        return services;
    }
}
