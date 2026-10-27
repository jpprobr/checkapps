using CheckApps.Application.Analysis;
using CheckApps.Application.Persistence;
using CheckApps.Domain.Analysis;

namespace CheckApps.Api.Contracts;

public static class IntegrationDtoMapping
{
    public static AppIntegrationsDto ToDto(this AppIntegrations integrations) => new(
        integrations.AppId,
        integrations.Repositories.Select(r => new RepositoryIntegrationsDto(
            r.Repository.Id, r.Repository.Name, r.Repository.Status, r.Repository.Role, r.Repository.Url, r.Repository.Sigla,
            r.Analysis?.ToDto(), r.LastFailure?.ToDto(),
            r.Sections.Select(s => new IntegrationSectionDto(s.Reference, s.Integrations.Select(i => i.ToDto()).ToList())).ToList())).ToList());

    public static IntegrationDto ToDto(this RepositoryIntegration i) => new(
        i.Id, i.Mechanism, i.Scheme, i.Host, i.Port, i.BasePath, i.Reference, i.ConfigKey, i.Environment, i.EnvironmentKind,
        i.State, i.ProjectPath, i.SourceFile, i.Origin, i.Confidence, i.ConfidenceNote, i.ScannerId, i.ScannerVersion);

    public static HostListDto ToDto(this HostListResult result) => new(
        result.Hosts.Select(h => new HostSummaryDto(
            h.Key, h.Host, h.Port, h.IsTemplate, h.AppCount, h.RepositoryCount,
            h.Squads.Select(s => new SquadRefDto(s.Id, s.Name)).ToList(), h.References, h.EnvironmentKinds, h.LatestAnalyzedAt)).ToList(),
        result.Coverage.ToDto());

    public static HostDetailDto ToDto(this HostDetail detail) => new(
        detail.Key, detail.Host, detail.Port, detail.IsTemplate, detail.Usages.Select(u => u.ToDto()).ToList());

    private static HostUsageDto ToDto(this IntegrationUsageRow u) => new(
        u.AppId, u.AppName, u.SquadId, u.SquadName, u.RepositoryId, u.RepositoryName, u.RepositoryStatus,
        u.Integration.ToDto(), u.AnalyzedAt);
}
