using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using CheckApps.Infrastructure.DynamoDb;
using Microsoft.Extensions.Options;

namespace CheckApps.IntegrationTests.DynamoDb;

/// <summary>
/// Sobe um client apontando para o DynamoDB Local (docker compose up -d na raiz do repo)
/// e recria a tabela do zero antes da suíte, para isolar as execuções de teste.
/// </summary>
public class DynamoDbLocalFixture : IAsyncLifetime
{
    public const string TableName = "CheckAppsIntegrationTests";

    public IAmazonDynamoDB Client { get; } = new AmazonDynamoDBClient(
        new BasicAWSCredentials("local", "local"),
        new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8000" });

    public IOptions<DynamoDbOptions> Options { get; } = Microsoft.Extensions.Options.Options.Create(
        new DynamoDbOptions { TableName = TableName, ServiceUrl = "http://localhost:8000" });

    public async Task InitializeAsync()
    {
        var existing = await Client.ListTablesAsync();
        if (existing.TableNames.Contains(TableName))
        {
            await Client.DeleteTableAsync(TableName);
            await WaitUntilDeletedAsync();
        }

        await new DynamoDbTableBootstrapper(Client, Options).EnsureTableExistsAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task WaitUntilDeletedAsync()
    {
        while (true)
        {
            try
            {
                await Client.DescribeTableAsync(TableName);
                await Task.Delay(200);
            }
            catch (ResourceNotFoundException)
            {
                return;
            }
        }
    }
}
