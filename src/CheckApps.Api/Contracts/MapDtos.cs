namespace CheckApps.Api.Contracts;

// Spec 020. O mapa é derivado das integrações declaradas e das decisões humanas; só lê.

/// <param name="Kind"><c>app</c>, <c>externo</c> ou <c>sem-app</c> (um nó só, que reúne os hosts sem decisão).</param>
/// <param name="QualityLevel">A letra do nível de qualidade atual (só para app).</param>
public record MapNodeDto(string Id, string Kind, string Label, string? SquadId, string? SquadName, string? QualityLevel);

/// <param name="EnvironmentKind"><c>dev</c>, <c>hom</c>, <c>prod</c>; nulo = arquivo base ou ambiente desconhecido (vale para todos).</param>
public record MapEdgeDto(
    string FromId, string ToId, string? EnvironmentKind, int Count, IReadOnlyList<string> Hosts, bool SelfLoop);

public record NoScannerRepositoryDto(string RepositoryId, string RepositoryName, string AppId, string AppName);

/// <param name="NoScannerRepositories">Repositórios sem scanner compatível: o mapa não os enxerga.</param>
public record MapCoverageDto(
    int UnanalyzedRepositories, DateTimeOffset? LatestAnalyzedAt, IReadOnlyList<NoScannerRepositoryDto> NoScannerRepositories);

public record AppMapDto(IReadOnlyList<MapNodeDto> Nodes, IReadOnlyList<MapEdgeDto> Edges, MapCoverageDto Coverage);
