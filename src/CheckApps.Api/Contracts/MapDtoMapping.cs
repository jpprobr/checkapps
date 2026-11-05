using CheckApps.Application.Analysis;

namespace CheckApps.Api.Contracts;

public static class MapDtoMapping
{
    public static AppMapDto ToDto(this AppMap map) => new(
        map.Nodes.Select(n => new MapNodeDto(n.Id, n.Kind, n.Label, n.SquadId, n.SquadName, n.QualityLevel)).ToList(),
        map.Edges.Select(e => new MapEdgeDto(e.FromId, e.ToId, e.EnvironmentKind, e.Count, e.Hosts, e.SelfLoop)).ToList(),
        new MapCoverageDto(
            map.Coverage.UnanalyzedRepositories, map.Coverage.LatestAnalyzedAt,
            map.Coverage.NoScannerRepositories
                .Select(r => new NoScannerRepositoryDto(r.RepositoryId, r.RepositoryName, r.AppId, r.AppName)).ToList()));
}
