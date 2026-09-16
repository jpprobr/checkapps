using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CheckApps.Application.Persistence;
using CheckApps.Domain;
using Microsoft.Extensions.Options;
using Repository = CheckApps.Domain.Repository;
using static CheckApps.Infrastructure.DynamoDb.AttributeValueExtensions;

namespace CheckApps.Infrastructure.DynamoDb;

public class AppRepository(IAmazonDynamoDB client, IOptions<DynamoDbOptions> options) : IAppRepository
{
    private readonly string _tableName = options.Value.TableName;

    public async Task<App?> GetAsync(string appId, CancellationToken cancellationToken = default)
    {
        var response = await client.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [TableSchema.Pk] = S(Keys.AppPk(appId)),
                [TableSchema.Sk] = S(Keys.Metadata)
            }
        }, cancellationToken);

        return response.IsItemSet ? ToApp(response.Item) : null;
    }

    public async Task<IReadOnlyList<App>> ListBySquadAsync(string squadId, CancellationToken cancellationToken = default)
    {
        var response = await client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            IndexName = TableSchema.Gsi1Name,
            KeyConditionExpression = $"{TableSchema.Gsi1Pk} = :pk AND begins_with({TableSchema.Gsi1Sk}, :skPrefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = S(Keys.SquadPk(squadId)),
                [":skPrefix"] = S("APP#")
            }
        }, cancellationToken);

        return response.Items.Select(ToApp).ToList();
    }

    public async Task<IReadOnlyList<App>> ListByQualityLevelAsync(QualityLevel level, CancellationToken cancellationToken = default)
    {
        var response = await client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            IndexName = TableSchema.Gsi2Name,
            KeyConditionExpression = $"{TableSchema.Gsi2Pk} = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = S(Keys.AppGsi2Pk(level))
            }
        }, cancellationToken);

        return response.Items.Select(ToApp).ToList();
    }

    public async Task<IReadOnlyList<Repository>> GetRepositoriesAsync(string appId, CancellationToken cancellationToken = default)
    {
        var items = await QueryAppChildrenAsync(appId, Keys.RepositorySkPrefix, cancellationToken);
        return items.Select(ToRepository).ToList();
    }

    public async Task<IReadOnlyList<SonarProject>> GetSonarProjectsAsync(string appId, CancellationToken cancellationToken = default)
    {
        var items = await QueryAppChildrenAsync(appId, Keys.SonarProjectSkPrefix, cancellationToken);
        return items.Select(ToSonarProject).ToList();
    }

    public async Task SaveAsync(App app, CancellationToken cancellationToken = default)
    {
        await client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(app)
        }, cancellationToken);
    }

    public async Task<bool> TryCreateAsync(App app, CancellationToken cancellationToken = default)
    {
        try
        {
            await client.PutItemAsync(new PutItemRequest
            {
                TableName = _tableName,
                Item = ToItem(app),
                ConditionExpression = $"attribute_not_exists({TableSchema.Pk})"
            }, cancellationToken);
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    public async Task DeleteAllAsync(string appId, CancellationToken cancellationToken = default)
    {
        var response = await client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = $"{TableSchema.Pk} = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue> { [":pk"] = S(Keys.AppPk(appId)) }
        }, cancellationToken);

        foreach (var batch in response.Items.Chunk(25))
        {
            if (batch.Length == 0) continue;

            await client.BatchWriteItemAsync(new BatchWriteItemRequest
            {
                RequestItems = new Dictionary<string, List<WriteRequest>>
                {
                    [_tableName] = [.. batch.Select(item => new WriteRequest
                    {
                        DeleteRequest = new DeleteRequest
                        {
                            Key = new Dictionary<string, AttributeValue>
                            {
                                [TableSchema.Pk] = item[TableSchema.Pk],
                                [TableSchema.Sk] = item[TableSchema.Sk]
                            }
                        }
                    })]
                }
            }, cancellationToken);
        }
    }

    public async Task AddRepositoryAsync(Repository repository, CancellationToken cancellationToken = default)
    {
        await client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(repository)
        }, cancellationToken);
    }

    public async Task AddSonarProjectAsync(SonarProject sonarProject, CancellationToken cancellationToken = default)
    {
        await client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(sonarProject)
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<AppLink>> GetLinksAsync(string appId, CancellationToken cancellationToken = default)
    {
        var items = await QueryAppChildrenAsync(appId, Keys.AppLinkSkPrefix, cancellationToken);
        return items.Select(ToAppLink).ToList();
    }

    public async Task SaveLinkAsync(AppLink link, CancellationToken cancellationToken = default)
    {
        await client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = ToItem(link)
        }, cancellationToken);
    }

    public async Task DeleteLinkAsync(string appId, string linkId, CancellationToken cancellationToken = default)
    {
        await client.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [TableSchema.Pk] = S(Keys.AppPk(appId)),
                [TableSchema.Sk] = S(Keys.AppLinkSk(linkId))
            }
        }, cancellationToken);
    }

    private async Task<List<Dictionary<string, AttributeValue>>> QueryAppChildrenAsync(
        string appId, string skPrefix, CancellationToken cancellationToken)
    {
        var response = await client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = $"{TableSchema.Pk} = :pk AND begins_with({TableSchema.Sk}, :skPrefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = S(Keys.AppPk(appId)),
                [":skPrefix"] = S(skPrefix)
            }
        }, cancellationToken);

        return response.Items;
    }

    internal static Dictionary<string, AttributeValue> ToItem(App app)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            [TableSchema.Pk] = S(Keys.AppPk(app.Id)),
            [TableSchema.Sk] = S(Keys.Metadata),
            [TableSchema.Gsi1Pk] = S(Keys.SquadPk(app.SquadId)),
            [TableSchema.Gsi1Sk] = S(Keys.AppGsi1Sk(app.Id)),
            ["id"] = S(app.Id),
            ["squadId"] = S(app.SquadId),
            ["name"] = S(app.Name),
            ["tags"] = Strings(app.Tags)
        };

        if (app.Description is not null) item["description"] = S(app.Description);
        if (app.Criticality is not null) item["criticality"] = S(app.Criticality);
        if (app.Status is not null) item["status"] = S(app.Status);
        if (app.ExternalId is not null) item["externalId"] = S(app.ExternalId);

        if (app.CurrentQualityLevel is { } level)
        {
            item[TableSchema.Gsi2Pk] = S(Keys.AppGsi2Pk(level));
            item[TableSchema.Gsi2Sk] = S(Keys.AppGsi2Sk(app.Id));
            item["currentQualityLevel"] = S(level.ToString());
        }

        if (app.CurrentMetrics is not null) item["currentMetrics"] = QualityMetricsMapper.ToAttributeValue(app.CurrentMetrics);
        if (app.LastSnapshotAt is { } lastSnapshotAt) item["lastSnapshotAt"] = Iso(lastSnapshotAt);
        if (app.LastSnapshotId is not null) item["lastSnapshotId"] = S(app.LastSnapshotId);
        if (app.AssessmentRulesVersion is not null) item["assessmentRulesVersion"] = S(app.AssessmentRulesVersion);

        return item;
    }

    private static Dictionary<string, AttributeValue> ToItem(Repository repository) => new()
    {
        [TableSchema.Pk] = S(Keys.AppPk(repository.AppId)),
        [TableSchema.Sk] = S(Keys.RepositorySk(repository.Id)),
        ["id"] = S(repository.Id),
        ["appId"] = S(repository.AppId),
        ["name"] = S(repository.Name),
        ["provider"] = repository.Provider is not null ? S(repository.Provider) : new AttributeValue { NULL = true },
        ["url"] = repository.Url is not null ? S(repository.Url) : new AttributeValue { NULL = true },
        ["externalId"] = repository.ExternalId is not null ? S(repository.ExternalId) : new AttributeValue { NULL = true },
        ["status"] = repository.Status is not null ? S(repository.Status) : new AttributeValue { NULL = true }
    };

    private static Dictionary<string, AttributeValue> ToItem(SonarProject sonarProject) => new()
    {
        [TableSchema.Pk] = S(Keys.AppPk(sonarProject.AppId)),
        [TableSchema.Sk] = S(Keys.SonarProjectSk(sonarProject.Id)),
        ["id"] = S(sonarProject.Id),
        ["appId"] = S(sonarProject.AppId),
        ["projectKey"] = S(sonarProject.ProjectKey),
        ["name"] = sonarProject.Name is not null ? S(sonarProject.Name) : new AttributeValue { NULL = true },
        ["url"] = sonarProject.Url is not null ? S(sonarProject.Url) : new AttributeValue { NULL = true },
        ["externalId"] = sonarProject.ExternalId is not null ? S(sonarProject.ExternalId) : new AttributeValue { NULL = true },
        ["status"] = sonarProject.Status is not null ? S(sonarProject.Status) : new AttributeValue { NULL = true },
        ["source"] = S(sonarProject.Source)
    };

    private static App ToApp(Dictionary<string, AttributeValue> item) => new()
    {
        Id = item.GetString("id"),
        SquadId = item.GetString("squadId"),
        Name = item.GetString("name"),
        Description = item.GetOptionalString("description"),
        Criticality = item.GetOptionalString("criticality"),
        Tags = item.GetStringSet("tags"),
        Status = item.GetOptionalString("status"),
        ExternalId = item.GetOptionalString("externalId"),
        CurrentQualityLevel = item.TryGetValue("currentQualityLevel", out var level)
            ? Enum.Parse<QualityLevel>(level.S) : null,
        CurrentMetrics = item.TryGetValue("currentMetrics", out var metrics)
            ? QualityMetricsMapper.FromAttributeValue(metrics) : null,
        LastSnapshotAt = item.GetOptionalDateTimeOffset("lastSnapshotAt"),
        LastSnapshotId = item.GetOptionalString("lastSnapshotId"),
        AssessmentRulesVersion = item.GetOptionalString("assessmentRulesVersion")
    };

    private static Repository ToRepository(Dictionary<string, AttributeValue> item) => new()
    {
        Id = item.GetString("id"),
        AppId = item.GetString("appId"),
        Name = item.GetString("name"),
        Provider = item.GetOptionalString("provider"),
        Url = item.GetOptionalString("url"),
        ExternalId = item.GetOptionalString("externalId"),
        Status = item.GetOptionalString("status")
    };

    private static SonarProject ToSonarProject(Dictionary<string, AttributeValue> item) => new()
    {
        Id = item.GetString("id"),
        AppId = item.GetString("appId"),
        ProjectKey = item.GetString("projectKey"),
        Name = item.GetOptionalString("name"),
        Url = item.GetOptionalString("url"),
        ExternalId = item.GetOptionalString("externalId"),
        Status = item.GetOptionalString("status"),
        Source = item.GetString("source")
    };

    private static Dictionary<string, AttributeValue> ToItem(AppLink link) => new()
    {
        [TableSchema.Pk] = S(Keys.AppPk(link.AppId)),
        [TableSchema.Sk] = S(Keys.AppLinkSk(link.Id)),
        ["id"] = S(link.Id),
        ["appId"] = S(link.AppId),
        ["description"] = S(link.Description),
        ["uri"] = S(link.Uri)
    };

    private static AppLink ToAppLink(Dictionary<string, AttributeValue> item) => new()
    {
        Id = item.GetString("id"),
        AppId = item.GetString("appId"),
        Description = item.GetString("description"),
        Uri = item.GetString("uri")
    };
}
