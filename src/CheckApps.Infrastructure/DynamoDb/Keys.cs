using CheckApps.Domain;

namespace CheckApps.Infrastructure.DynamoDb;

/// <summary>
/// Constrói os valores de PK/SK/GSI usados por cada tipo de item (seção 4 do plano).
/// Centralizar aqui evita strings de chave espalhadas e divergentes pelos repositórios.
/// </summary>
public static class Keys
{
    public const string Metadata = "METADATA";
    public const string SquadListPartition = "SQUAD";

    public static string SquadPk(string squadId) => $"SQUAD#{squadId}";
    public static string SquadGsi1Sk(string squadId) => $"SQUAD#{squadId}";

    public static string AppPk(string appId) => $"APP#{appId}";
    public static string AppGsi1Sk(string appId) => $"APP#{appId}";
    public static string AppGsi2Pk(QualityLevel level) => $"QUALITYLEVEL#{level}";
    public static string AppGsi2Sk(string appId) => $"APP#{appId}";

    public static string RepositorySk(string repositoryId) => $"REPO#{repositoryId}";
    public const string RepositorySkPrefix = "REPO#";

    public static string SonarProjectSk(string sonarProjectId) => $"SONARPROJECT#{sonarProjectId}";
    public const string SonarProjectSkPrefix = "SONARPROJECT#";

    public static string AppLinkSk(string linkId) => $"APPLINK#{linkId}";
    public const string AppLinkSkPrefix = "APPLINK#";

    public const string SnapshotSkPrefix = "SNAPSHOT#";

    public static string SnapshotSk(DateTimeOffset collectedAt, string sonarProjectId) =>
        $"{SnapshotSkPrefix}{FormatTimestamp(collectedAt)}#{sonarProjectId}";

    public static string SnapshotSkLowerBound(DateTimeOffset from) => $"{SnapshotSkPrefix}{FormatTimestamp(from)}";

    public static string SnapshotSkUpperBound(DateTimeOffset to) => $"{SnapshotSkPrefix}{FormatTimestamp(to)}#￿";

    private static string FormatTimestamp(DateTimeOffset value) => value.ToUniversalTime().ToString("O");
}
