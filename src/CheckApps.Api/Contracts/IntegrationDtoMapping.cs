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
            r.Sections.Select(s => new IntegrationSectionDto(
                s.Reference, s.Integrations.Select(i => i.ToDto()).ToList(),
                s.Destinations.ToDictionary(d => d.Key, d => d.Value.ToDto()),
                s.Externals.ToDictionary(d => d.Key, d => d.Value.ToDto()))).ToList())).ToList());

    public static IntegrationDto ToDto(this RepositoryIntegration i) => new(
        i.Id, i.Mechanism, i.Scheme, i.Host, i.Port, i.BasePath, i.Reference, i.ConfigKey, i.Environment, i.EnvironmentKind,
        i.State, i.ProjectPath, i.SourceFile, i.Origin, i.Confidence, i.ConfidenceNote, i.ScannerId, i.ScannerVersion);

    public static HostListDto ToDto(this HostListResult result) => new(
        result.Hosts.Select(h => new HostSummaryDto(
            h.Key, h.Host, h.Port, h.IsTemplate, h.AppCount, h.RepositoryCount,
            h.Squads.Select(s => new SquadRefDto(s.Id, s.Name)).ToList(), h.References, h.EnvironmentKinds, h.LatestAnalyzedAt, h.Link.ToDto())).ToList(),
        result.Coverage.ToDto());

    public static HostDetailDto ToDto(this HostDetail detail) => new(
        detail.Key, detail.Host, detail.Port, detail.IsTemplate, detail.Usages.Select(u => u.ToDto()).ToList(), detail.Link.ToDto());

    public static HostLinkMatchDto ToDto(this HostLinkMatch m) => new(
        m.LinkId, m.HostKey, m.AppId, m.AppName, m.State, m.Via, m.Template, m.DecidedAt, m.Note);

    public static HostLinkInfoDto ToDto(this HostLinkInfo info) => new(
        info.Status, info.Confirmed?.ToDto(), info.Rejected.Select(r => r.ToDto()).ToList(), info.External?.ToDto());

    public static HostExternalMatchDto ToDto(this HostExternalMatch m) => new(
        m.ExternalId, m.HostKey, m.Name, m.Via, m.Template, m.DecidedAt, m.Note);

    public static BatchHostResultDto ToDto(this HostBatchResult r) => new(r.HostKey, r.Ok, r.Error);

    public static HostLinkDto ToDto(this HostLinkListItem l) => new(
        l.Id, l.HostKey, l.IsTemplate, l.AppId, l.AppName, l.State, l.Origin, l.DecidedAt, l.Note, l.Seen);

    private static HostUsageDto ToDto(this IntegrationUsageRow u) => new(
        u.AppId, u.AppName, u.SquadId, u.SquadName, u.RepositoryId, u.RepositoryName, u.RepositoryStatus,
        u.Integration.ToDto(), u.AnalyzedAt);
}
