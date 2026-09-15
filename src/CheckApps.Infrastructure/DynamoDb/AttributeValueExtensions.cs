using System.Globalization;
using Amazon.DynamoDBv2.Model;

namespace CheckApps.Infrastructure.DynamoDb;

internal static class AttributeValueExtensions
{
    public static string GetString(this Dictionary<string, AttributeValue> item, string key) => item[key].S;

    public static string? GetOptionalString(this Dictionary<string, AttributeValue> item, string key) =>
        item.TryGetValue(key, out var value) && value.NULL != true ? value.S : null;

    public static double GetDouble(this Dictionary<string, AttributeValue> item, string key) =>
        double.Parse(item[key].N, CultureInfo.InvariantCulture);

    public static int GetInt(this Dictionary<string, AttributeValue> item, string key) =>
        int.Parse(item[key].N, CultureInfo.InvariantCulture);

    public static DateTimeOffset GetDateTimeOffset(this Dictionary<string, AttributeValue> item, string key) =>
        DateTimeOffset.Parse(item[key].S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public static DateTimeOffset? GetOptionalDateTimeOffset(this Dictionary<string, AttributeValue> item, string key) =>
        item.TryGetValue(key, out var value) && value.NULL != true
            ? DateTimeOffset.Parse(value.S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            : null;

    public static IReadOnlyCollection<string> GetStringSet(this Dictionary<string, AttributeValue> item, string key) =>
        item.TryGetValue(key, out var value) && value.NULL != true && value.SS is { Count: > 0 } ss ? ss : [];

    public static AttributeValue S(string value) => new() { S = value };

    public static AttributeValue N(double value) => new() { N = value.ToString(CultureInfo.InvariantCulture) };

    public static AttributeValue Iso(DateTimeOffset value) => new() { S = value.ToUniversalTime().ToString("O") };

    public static AttributeValue Strings(IReadOnlyCollection<string> values) =>
        values.Count > 0 ? new AttributeValue { SS = [.. values] } : new AttributeValue { NULL = true };
}
