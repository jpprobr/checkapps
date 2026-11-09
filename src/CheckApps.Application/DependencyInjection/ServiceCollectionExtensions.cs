using CheckApps.Application.Analysis;
using CheckApps.Application.Apps;
using CheckApps.Application.Checks;
using CheckApps.Application.Discovery;
using CheckApps.Application.QualitySnapshots;
using CheckApps.Application.Squads;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CheckApps.Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCheckAppsApplication(this IServiceCollection services)
    {
        services.AddScoped<SquadService>();
        services.AddScoped<AppService>();
        services.AddScoped<QualitySnapshotService>();
        services.AddScoped<CheckService>();
        // ScanService fica de fora de propósito: depende de IRepositoryDiscoverySource, que só a
        // ferramenta de scan registra. Registrá-lo aqui quebraria a API (validação de DI em Development).
        services.AddScoped<TriageService>();
        services.AddScoped<DependencyService>();
        services.AddScoped<IntegrationService>();
        services.AddScoped<ContractService>();
        services.AddScoped<MapService>();
        services.AddScoped<HostLinkService>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
