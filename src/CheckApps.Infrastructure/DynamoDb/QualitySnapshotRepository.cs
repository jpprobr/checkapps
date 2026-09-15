using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CheckApps.Application.Persistence;
using CheckApps.Domain;
using Microsoft.Extensions.Options;
using static CheckApps.Infrastructure.DynamoDb.AttributeValueExtensions;

namespace CheckApps.Infrastructure.DynamoDb;

public class QualitySnapshotRepository(IAmazonDynamoDB client, IOptions<DynamoDbOptions> options) : IQualitySnapshotRepository
{
    private readonly string _tableName = options.Value.TableName;

    public async Task AddAsync(QualitySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var putSnapshot = new Put
        {
            TableName = _tableName,
            Item = ToItem(snapshot)
        };

        var updateApp = new Update
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                [TableSchema.Pk] = S(Keys.AppPk(snapshot.AppId)),
                [TableSchema.Sk] = S(Keys.Metadata)
            },
            UpdateExpression =
                $"SET currentQualityLevel = :level, currentMetrics = :metrics, " +
                $"lastSnapshotAt = :collectedAt, lastSnapshotId = :snapshotId, " +
                $"assessmentRulesVersion = :rulesVersion, {TableSchema.Gsi2Pk} = :gsi2pk, {TableSchema.Gsi2Sk} = :gsi2sk",
            ConditionExpression = "attribute_not_exists(lastSnapshotAt) OR lastSnapshotAt < :collectedAt",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":level"] = S(snapshot.QualityLevel.ToString()),
                [":metrics"] = QualityMetricsMapper.ToAttributeValue(snapshot.Metrics),
                [":collectedAt"] = Iso(snapshot.CollectedAt),
                [":snapshotId"] = S(snapshot.Id),
                [":rulesVersion"] = S(snapshot.AssessmentRulesVersion),
                [":gsi2pk"] = S(Keys.AppGsi2Pk(snapshot.QualityLevel)),
                [":gsi2sk"] = S(Keys.AppGsi2Sk(snapshot.AppId))
            }
        };

        try
        {
            await client.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                TransactItems =
                [
                    new TransactWriteItem { Put = putSnapshot },
                    new TransactWriteItem { Update = updateApp }
                ]
            }, cancellationToken);
        }
        catch (TransactionCanceledException ex) when (IsOnlyTheCurrentStateUpdateThatFailed(ex))
        {
            // Snapshot mais antigo que o último registrado: grava só o snapshot, sem
            // regredir o estado atual do App (backfill fora de ordem é esperado).
            await client.PutItemAsync(new PutItemRequest { TableName = _tableName, Item = putSnapshot.Item }, cancellationToken);
        }
    }

    private static bool IsOnlyTheCurrentStateUpdateThatFailed(TransactionCanceledException ex) =>
        ex.CancellationReasons is [{ Code: "None" }, { Code: "ConditionalCheckFailed" }];

    public async Task<IReadOnlyList<QualitySnapshot>> GetHistoryAsync(
        string appId, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default)
    {
        var keyCondition = $"{TableSchema.Pk} = :pk AND ";
        var values = new Dictionary<string, AttributeValue> { [":pk"] = S(Keys.AppPk(appId)) };

        if (from is null && to is null)
        {
            keyCondition += $"begins_with({TableSchema.Sk}, :skPrefix)";
            values[":skPrefix"] = S(Keys.SnapshotSkPrefix);
        }
        else
        {
            keyCondition += $"{TableSchema.Sk} BETWEEN :skFrom AND :skTo";
            values[":skFrom"] = S(from is { } f ? Keys.SnapshotSkLowerBound(f) : Keys.SnapshotSkPrefix);
            values[":skTo"] = S(to is { } t ? Keys.SnapshotSkUpperBound(t) : Keys.SnapshotSkPrefix + "￿");
        }

        var response = await client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = keyCondition,
            ExpressionAttributeValues = values
        }, cancellationToken);

        return response.Items.Select(ToSnapshot).OrderBy(s => s.CollectedAt).ToList();
    }

    public async Task<QualitySnapshot?> GetLatestAsync(string appId, CancellationToken cancellationToken = default)
    {
        var response = await client.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = $"{TableSchema.Pk} = :pk AND begins_with({TableSchema.Sk}, :skPrefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = S(Keys.AppPk(appId)),
                [":skPrefix"] = S(Keys.SnapshotSkPrefix)
            },
            ScanIndexForward = false,
            Limit = 1
        }, cancellationToken);

        return response.Items.Count > 0 ? ToSnapshot(response.Items[0]) : null;
    }

    private static Dictionary<string, AttributeValue> ToItem(QualitySnapshot snapshot) => new()
    {
        [TableSchema.Pk] = S(Keys.AppPk(snapshot.AppId)),
        [TableSchema.Sk] = S(Keys.SnapshotSk(snapshot.CollectedAt, snapshot.SonarProjectId)),
        ["id"] = S(snapshot.Id),
        ["appId"] = S(snapshot.AppId),
        ["sonarProjectId"] = S(snapshot.SonarProjectId),
        ["collectedAt"] = Iso(snapshot.CollectedAt),
        ["source"] = S(snapshot.Source),
        ["metrics"] = QualityMetricsMapper.ToAttributeValue(snapshot.Metrics),
        ["qualityLevel"] = S(snapshot.QualityLevel.ToString()),
        ["assessmentRulesVersion"] = S(snapshot.AssessmentRulesVersion)
    };

    private static QualitySnapshot ToSnapshot(Dictionary<string, AttributeValue> item) => new()
    {
        Id = item.GetString("id"),
        AppId = item.GetString("appId"),
        SonarProjectId = item.GetString("sonarProjectId"),
        CollectedAt = item.GetDateTimeOffset("collectedAt"),
        Source = item.GetString("source"),
        Metrics = QualityMetricsMapper.FromAttributeValue(item["metrics"]),
        QualityLevel = Enum.Parse<QualityLevel>(item.GetString("qualityLevel")),
        AssessmentRulesVersion = item.GetString("assessmentRulesVersion")
    };
}
