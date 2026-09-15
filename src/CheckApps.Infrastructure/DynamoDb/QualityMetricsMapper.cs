using Amazon.DynamoDBv2.Model;
using CheckApps.Domain;
using static CheckApps.Infrastructure.DynamoDb.AttributeValueExtensions;

namespace CheckApps.Infrastructure.DynamoDb;

internal static class QualityMetricsMapper
{
    public static AttributeValue ToAttributeValue(QualityMetrics metrics) => new()
    {
        M = new Dictionary<string, AttributeValue>
        {
            ["coverage"] = N(metrics.Coverage),
            ["bugs"] = N(metrics.Bugs),
            ["vulnerabilities"] = N(metrics.Vulnerabilities),
            ["codeSmells"] = N(metrics.CodeSmells),
            ["duplications"] = N(metrics.Duplications),
            ["reliability"] = S(metrics.Reliability),
            ["maintainability"] = S(metrics.Maintainability),
            ["security"] = S(metrics.Security)
        }
    };

    public static QualityMetrics FromAttributeValue(AttributeValue value)
    {
        var m = value.M;
        return new QualityMetrics
        {
            Coverage = m.GetDouble("coverage"),
            Bugs = m.GetInt("bugs"),
            Vulnerabilities = m.GetInt("vulnerabilities"),
            CodeSmells = m.GetInt("codeSmells"),
            Duplications = m.GetDouble("duplications"),
            Reliability = m.GetString("reliability"),
            Maintainability = m.GetString("maintainability"),
            Security = m.GetString("security")
        };
    }
}
