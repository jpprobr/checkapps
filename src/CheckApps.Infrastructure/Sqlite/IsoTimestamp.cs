using System.Globalization;

namespace CheckApps.Infrastructure.Sqlite;

/// <summary>
/// Datas são gravadas como TEXT ISO-8601 UTC, exatamente como eram no DynamoDB — é o
/// que mantém a serialização da API idêntica e o schema portável para Postgres.
/// </summary>
internal static class IsoTimestamp
{
    public static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("O");

    public static DateTimeOffset Parse(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
