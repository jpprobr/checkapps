using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Options;

namespace CheckApps.Infrastructure.DynamoDb;

/// <summary>
/// Cria a tabela única do CheckApps (PK/SK + GSI1 + GSI2) se ela ainda não existir.
/// Idempotente: pensada para rodar contra o DynamoDB Local em dev, ou uma única vez
/// contra a conta AWS pessoal.
/// </summary>
public class DynamoDbTableBootstrapper(IAmazonDynamoDB client, IOptions<DynamoDbOptions> options)
{
    private readonly string _tableName = options.Value.TableName;

    public async Task EnsureTableExistsAsync(CancellationToken cancellationToken = default)
    {
        var existingTables = await client.ListTablesAsync(cancellationToken);
        if (existingTables.TableNames.Contains(_tableName))
        {
            return;
        }

        var request = new CreateTableRequest
        {
            TableName = _tableName,
            BillingMode = BillingMode.PAY_PER_REQUEST,
            AttributeDefinitions =
            [
                new AttributeDefinition(TableSchema.Pk, ScalarAttributeType.S),
                new AttributeDefinition(TableSchema.Sk, ScalarAttributeType.S),
                new AttributeDefinition(TableSchema.Gsi1Pk, ScalarAttributeType.S),
                new AttributeDefinition(TableSchema.Gsi1Sk, ScalarAttributeType.S),
                new AttributeDefinition(TableSchema.Gsi2Pk, ScalarAttributeType.S),
                new AttributeDefinition(TableSchema.Gsi2Sk, ScalarAttributeType.S)
            ],
            KeySchema =
            [
                new KeySchemaElement(TableSchema.Pk, KeyType.HASH),
                new KeySchemaElement(TableSchema.Sk, KeyType.RANGE)
            ],
            GlobalSecondaryIndexes =
            [
                new GlobalSecondaryIndex
                {
                    IndexName = TableSchema.Gsi1Name,
                    Projection = new Projection { ProjectionType = ProjectionType.ALL },
                    KeySchema =
                    [
                        new KeySchemaElement(TableSchema.Gsi1Pk, KeyType.HASH),
                        new KeySchemaElement(TableSchema.Gsi1Sk, KeyType.RANGE)
                    ]
                },
                new GlobalSecondaryIndex
                {
                    IndexName = TableSchema.Gsi2Name,
                    Projection = new Projection { ProjectionType = ProjectionType.ALL },
                    KeySchema =
                    [
                        new KeySchemaElement(TableSchema.Gsi2Pk, KeyType.HASH),
                        new KeySchemaElement(TableSchema.Gsi2Sk, KeyType.RANGE)
                    ]
                }
            ]
        };

        await client.CreateTableAsync(request, cancellationToken);
        await WaitUntilActiveAsync(cancellationToken);
    }

    private async Task WaitUntilActiveAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var description = await client.DescribeTableAsync(_tableName, cancellationToken);
            if (description.Table.TableStatus == TableStatus.ACTIVE)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }
    }
}
