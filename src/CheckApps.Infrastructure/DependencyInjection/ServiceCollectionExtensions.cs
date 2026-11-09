using CheckApps.Application.Analysis;
using CheckApps.Application.Discovery;
using CheckApps.Application.Persistence;
using CheckApps.Application.Sonar;
using CheckApps.Domain.QualityAssessment;
using CheckApps.Infrastructure.Analysis;
using CheckApps.Infrastructure.Analysis.DotNet;
using CheckApps.Infrastructure.Analysis.OpenApi;
using CheckApps.Infrastructure.Discovery;
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
        AddRepositoryDiscovery(services, configuration);
        AddRepositoryAnalysis(services, configuration);
        services.AddSingleton(configuration.GetSection(AnalysisOptions.SectionName).Get<AnalysisOptions>() ?? new AnalysisOptions());

        services.AddSingleton<SqliteConnectionFactory>();
        services.AddSingleton<SqliteSchemaBootstrapper>();

        services.AddSingleton<ISquadRepository, SquadRepository>();
        services.AddSingleton<IAppRepository, AppRepository>();
        services.AddSingleton<IQualitySnapshotRepository, QualitySnapshotRepository>();
        services.AddSingleton<ICheckRepository, CheckRepository>();
        services.AddSingleton<IDiscoveredRepositoryRepository, DiscoveredRepositoryRepository>();
        services.AddSingleton<IAnalysisFactWriter, DependencyFactWriter>();
        services.AddSingleton<IAnalysisFactWriter, IntegrationFactWriter>();
        services.AddSingleton<IAnalysisFactWriter, ApiContractFactWriter>();
        services.AddSingleton<IAnalysisRepository, AnalysisRepository>();
        services.AddSingleton<IHostLinkRepository, HostLinkRepository>();
        services.AddSingleton<IHostExternalRepository, HostExternalRepository>();

        services.AddSingleton<IQualityAssessmentEngine, QualityAssessmentEngineV1>();
        services.AddSingleton<ISonarProvider, MockSonarProvider>();

        return services;
    }

    /// <summary>
    /// Composição da análise (spec 013), usada pela ferramenta <c>CheckApps.Analyze</c>: scanners, fonte de conteúdo
    /// escolhida pela mesma chave <c>Scan:Source</c> da descoberta (<c>github</c> | <c>fake</c>) e o
    /// <see cref="AnalysisService"/>. Como na descoberta, a fonte nunca falha na construção: sem credencial ela expõe
    /// <c>ConfigurationProblem</c>. A API só lê: nenhum endpoint dela usa o <see cref="AnalysisService"/> (FR-015).
    /// </summary>
    private static void AddRepositoryAnalysis(IServiceCollection services, IConfiguration configuration)
    {
        var source = configuration.GetSection(ScanOptions.SectionName).Get<ScanOptions>()?.Source ?? "github";

        services.AddSingleton<IRepositoryScanner, DotNetScanner>();
        services.AddSingleton<IRepositoryScanner, OpenApiScanner>();
        services.AddSingleton<ScannerRegistry>();
        services.AddSingleton<IAnalysisFactProcessor, DependencyFactProcessor>();
        services.AddSingleton<IAnalysisFactProcessor, IntegrationFactProcessor>();
        services.AddSingleton<IAnalysisFactProcessor, ApiContractFactProcessor>();
        services.AddHttpClient<GitHubRepositoryContentSource>();

        if (string.Equals(source, "fake", StringComparison.OrdinalIgnoreCase))
        {
            var revision = configuration.GetSection(ScanOptions.SectionName).Get<ScanOptions>()?.FakeRevision ?? 0;
            services.AddSingleton<IRepositoryContentSource, FakeRepositoryContentSource>(_ => new FakeRepositoryContentSource(revision));
        }
        else
        {
            services.AddScoped<IRepositoryContentSource>(sp => sp.GetRequiredService<GitHubRepositoryContentSource>());
        }

        services.AddScoped<AnalysisService>();
    }

    /// <summary>
    /// Composição única do scan, usada pela API e pela ferramenta de linha de comando: opções, fonte
    /// escolhida por <c>Scan:Source</c> (<c>github</c> | <c>fake</c>) e <see cref="ScanService"/>.
    /// A fonte nunca falha na construção — sem credencial ela expõe <c>ConfigurationProblem</c> —,
    /// então a API sobe sem token e a tela explica o motivo.
    /// </summary>
    private static void AddRepositoryDiscovery(IServiceCollection services, IConfiguration configuration)
    {
        var scanOptions = configuration.GetSection(ScanOptions.SectionName).Get<ScanOptions>() ?? new ScanOptions();
        var gitHubOptions = configuration.GetSection(GitHubOptions.SectionName).Get<GitHubOptions>() ?? new GitHubOptions();

        var useFake = string.Equals(scanOptions.Source, "fake", StringComparison.OrdinalIgnoreCase);
        if (!useFake && !string.Equals(scanOptions.Source, "github", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{ScanOptions.SectionName}:{nameof(ScanOptions.Source)} deve ser github ou fake (valor atual: \"{scanOptions.Source}\").");
        }

        // Demonstração sem configurar nada: a fonte fake monta os nomes a partir de uma convenção fictícia.
        if (useFake && string.IsNullOrWhiteSpace(scanOptions.NameTemplate))
        {
            scanOptions.NameTemplate = "fake-{sigla}-";
        }

        services.AddSingleton(scanOptions);
        services.AddSingleton(gitHubOptions);
        services.AddHttpClient<GitHubRepositorySource>();

        if (useFake)
        {
            services.AddSingleton<IRepositoryDiscoverySource, FakeRepositorySource>();
        }
        else
        {
            services.AddScoped<IRepositoryDiscoverySource>(sp => sp.GetRequiredService<GitHubRepositorySource>());
        }

        services.AddScoped<ScanService>();
    }
}
