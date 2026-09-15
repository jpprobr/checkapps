using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CheckApps.Application.Persistence;
using CheckApps.Domain;
using Microsoft.Extensions.Options;
using static CheckApps.Infrastructure.DynamoDb.AttributeValueExtensions;

namespace CheckApps.Infrastructure.DynamoDb;

public class SquadRepository(IAmazonDynamoDB client, IOptions<DynamoDbOptions> options) : ISquadRepository
{
    private readonly string _tableName = options.Value.TableName;

    public async Task<IReadOnlyList<Squad>> ListAsync(CancellationToken cancellationToken = default)
    {
        var response = await client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            IndexName = TableSchema.Gsi1Name,
            KeyConditionExpression = $"{TableSchema.Gsi1Pk} = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = S(Keys.SquadListPartition)
            }
        }, cancellationToken);

        return response.Items.Select(ToSquad).ToList();
    }

    public async Task<Squad?> GetAsync(string squadId, CancellationToken cancellationToken = default)
    {
        var response = await client.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [TableSchema.Pk] = S(Keys.SquadPk(squadId)),
                [TableSchema.Sk] = S(Keys.Metadata)
            }
        }, cancellationToken);

        return response.IsItemSet ? ToSquad(response.Item) : null;
    }

    public async Task SaveAsync(Squad squad, CancellationToken cancellationToken = default)
    {
        await client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(squad)
        }, cancellationToken);
    }

    private static Dictionary<string, AttributeValue> ToItem(Squad squad)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            [TableSchema.Pk] = S(Keys.SquadPk(squad.Id)),
            [TableSchema.Sk] = S(Keys.Metadata),
            [TableSchema.Gsi1Pk] = S(Keys.SquadListPartition),
            [TableSchema.Gsi1Sk] = S(Keys.SquadGsi1Sk(squad.Id)),
            ["id"] = S(squad.Id),
            ["name"] = S(squad.Name)
        };

        if (squad.Description is not null)
        {
            item["description"] = S(squad.Description);
        }

        if (squad.Status is not null)
        {
            item["status"] = S(squad.Status);
        }

        return item;
    }

    private static Squad ToSquad(Dictionary<string, AttributeValue> item) => new()
    {
        Id = item.GetString("id"),
        Name = item.GetString("name"),
        Description = item.GetOptionalString("description"),
        Status = item.GetOptionalString("status")
    };
}
