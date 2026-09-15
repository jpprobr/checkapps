using Amazon.DynamoDBv2;
using Amazon.Runtime;
using CheckApps.Application.Persistence;
using CheckApps.Application.Sonar;
using CheckApps.Domain.QualityAssessment;
using CheckApps.Infrastructure.DynamoDb;
using CheckApps.Infrastructure.Sonar;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CheckApps.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCheckAppsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DynamoDbOptions>(configuration.GetSection(DynamoDbOptions.SectionName));

        services.AddSingleton<IAmazonDynamoDB>(sp =>
        {
            var options = configuration.GetSection(DynamoDbOptions.SectionName).Get<DynamoDbOptions>() ?? new DynamoDbOptions();

            var config = new AmazonDynamoDBConfig { RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(options.Region) };
            if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
            {
                config.ServiceURL = options.ServiceUrl;
                return new AmazonDynamoDBClient(new BasicAWSCredentials("local", "local"), config);
            }

            return new AmazonDynamoDBClient(config);
        });

        services.AddSingleton<DynamoDbTableBootstrapper>();
        services.AddSingleton<ISquadRepository, SquadRepository>();
        services.AddSingleton<IAppRepository, AppRepository>();
        services.AddSingleton<IQualitySnapshotRepository, QualitySnapshotRepository>();

        services.AddSingleton<IQualityAssessmentEngine, QualityAssessmentEngineV1>();
        services.AddSingleton<ISonarProvider, MockSonarProvider>();

        return services;
    }
}
