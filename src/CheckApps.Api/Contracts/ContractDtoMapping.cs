using CheckApps.Application.Analysis;
using CheckApps.Domain.Analysis;

namespace CheckApps.Api.Contracts;

public static class ContractDtoMapping
{
    public static AppContractsDto ToDto(this AppContracts contracts) => new(
        contracts.AppId,
        contracts.Repositories.Select(r => new RepositoryContractsDto(
            r.Repository.Id, r.Repository.Name, r.Repository.Status, r.Repository.Role, r.Repository.Url, r.DeclaredIn,
            r.Analysis?.ToDto(), r.LastFailure?.ToDto(), r.Endpoints.Select(e => e.ToDto()).ToList())).ToList());

    public static ApiEndpointDto ToDto(this RepositoryApiEndpoint e) => new(
        e.Id, e.Mechanism, e.Method, e.Path, e.DeclaredId, e.Summary, e.Tags, e.Security, e.Deprecated, e.ApiTitle, e.ApiVersion,
        e.SourceFile, e.Origin, e.Confidence, e.ConfidenceNote, e.ScannerId, e.ScannerVersion,
        e.Fields.Select(f => new ApiFieldDto(f.Location, f.StatusCode, f.MediaType, f.Path, f.Type, f.Format, f.Required, f.EnumValues, f.Note)).ToList());

    public static AppContractChangesDto ToDto(this AppContractChanges changes) => new(
        changes.AppId,
        changes.Repositories.Select(r => new RepositoryContractChangesDto(
            r.Repository.Id, r.Repository.Name, r.State, r.Analysis?.ToDto(), r.Baseline?.ToDto(),
            r.Changes.Select(c => new ContractChangeDto(
                c.Severity, c.Kind, c.Method, c.Path, c.Location, c.StatusCode, c.MediaType, c.FieldPath, c.Description)).ToList(),
            r.Warnings)).ToList());
}
