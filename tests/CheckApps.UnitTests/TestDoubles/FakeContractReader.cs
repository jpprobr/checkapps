using CheckApps.Application.Analysis;
using CheckApps.Domain;
using CheckApps.Domain.Analysis;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.UnitTests.TestDoubles;

/// <summary>
/// Contratos e mudanças prontos por app, para testar as jornadas (spec 022) sem banco nem análise. App que não foi configurado devolve
/// "sem contrato", como o serviço real faz para um app que nunca foi analisado.
/// </summary>
public class FakeContractReader : IContractReader
{
    private readonly Dictionary<string, AppContracts> _contracts = [];
    private readonly Dictionary<string, AppContractChanges> _changes = [];

    public FakeContractReader WithEndpoints(string appId, params RepositoryApiEndpoint[] endpoints)
    {
        _contracts[appId] = new AppContracts(appId, [new RepositoryContracts(
            new Repository { Id = $"{appId}-contrato", AppId = appId, Name = $"{appId}-contrato" }, null, null,
            ContractDeclaredIn.ContractRepository, endpoints)]);
        return this;
    }

    public FakeContractReader WithChanges(string appId, string state, params ContractChange[] changes)
    {
        _changes[appId] = new AppContractChanges(appId, [new RepositoryContractChanges(
            new Repository { Id = $"{appId}-contrato", AppId = appId, Name = $"{appId}-contrato" }, null, null, state, changes, [])]);
        return this;
    }

    public Task<AppContracts> GetForAppAsync(string appId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_contracts.GetValueOrDefault(appId) ?? new AppContracts(appId, []));

    public Task<AppContractChanges> GetChangesForAppAsync(string appId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_changes.GetValueOrDefault(appId) ?? new AppContractChanges(appId, []));

    public static RepositoryApiEndpoint Endpoint(string method, string path, string? summary = null, bool deprecated = false) =>
        new()
        {
            Id = Guid.NewGuid().ToString("n"), AnalysisId = "an", Mechanism = ApiMechanisms.OpenApi, Method = method, Path = path,
            PathKey = ApiPath.KeyOf(path), Summary = summary, Deprecated = deprecated, SourceFile = "openapi.yaml",
            Confidence = Confidences.High, ScannerId = "openapi", ScannerVersion = "1.0.0", Security = ["oauth (oauth2)"]
        };

    public static ContractChange Change(
        string severity, string kind, string method, string path, string? field = null) =>
        new(severity, kind, method, path, field is null ? null : ApiFieldLocations.Request, null, null, field, "descrição");
}
