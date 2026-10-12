using CheckApps.Application.Persistence;
using CheckApps.Domain;
using Repository = CheckApps.Domain.Repository;

namespace CheckApps.Application.Apps;

public class AppService(
    IAppRepository apps, ISquadRepository squads, IQualitySnapshotRepository snapshots, ICheckRepository checks)
{
    public Task<App?> GetAppAsync(string appId, CancellationToken cancellationToken = default) =>
        apps.GetAsync(appId, cancellationToken);

    public Task<IReadOnlyList<Repository>> ListRepositoriesAsync(string appId, CancellationToken cancellationToken = default) =>
        apps.GetRepositoriesAsync(appId, cancellationToken);

    public Task<IReadOnlyList<SonarProject>> ListSonarProjectsAsync(string appId, CancellationToken cancellationToken = default) =>
        apps.GetSonarProjectsAsync(appId, cancellationToken);

    public Task<IReadOnlyList<App>> ListByQualityLevelAsync(QualityLevel level, CancellationToken cancellationToken = default) =>
        apps.ListByQualityLevelAsync(level, cancellationToken);

    public Task<IReadOnlyList<QualitySnapshot>> GetQualityHistoryAsync(
        string appId, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default) =>
        snapshots.GetHistoryAsync(appId, from, to, cancellationToken);

    public async Task<App> CreateAsync(
        string name, string squadId, string? description, string? criticality,
        IReadOnlyCollection<string>? tags, string? externalId, CancellationToken cancellationToken = default)
    {
        await EnsureSquadExistsAsync(squadId, cancellationToken);

        App? created = null;

        await SlugIdGenerator.GenerateUniqueAsync(name, async (candidateId, ct) =>
        {
            var app = new App
            {
                Id = candidateId,
                SquadId = squadId,
                Name = name,
                Description = description,
                Criticality = criticality,
                Tags = tags ?? [],
                Status = "active",
                ExternalId = externalId
            };

            if (!await apps.TryCreateAsync(app, ct))
            {
                return false;
            }

            created = app;
            return true;
        }, cancellationToken);

        return created!;
    }

    public async Task<App> UpdateAsync(
        string appId, string name, string squadId, string? description, string? criticality,
        IReadOnlyCollection<string>? tags, string? status, string? externalId, CancellationToken cancellationToken = default)
    {
        var app = await apps.GetAsync(appId, cancellationToken)
            ?? throw new KeyNotFoundException($"App '{appId}' não encontrado.");

        if (squadId != app.SquadId)
        {
            await EnsureSquadExistsAsync(squadId, cancellationToken);
        }

        // Muta o objeto carregado (não cria um App novo): preserva os campos
        // desnormalizados de quality state que SaveAsync grava de volta junto.
        app.Name = name;
        app.SquadId = squadId;
        app.Description = description;
        app.Criticality = criticality;
        app.Tags = tags ?? [];
        app.Status = status;
        app.ExternalId = externalId;

        await apps.SaveAsync(app, cancellationToken);
        return app;
    }

    public async Task DeleteAsync(string appId, CancellationToken cancellationToken = default)
    {
        var latestSnapshot = await snapshots.GetLatestAsync(appId, cancellationToken);
        if (latestSnapshot is not null)
        {
            throw new DomainConflictException(
                $"App '{appId}' possui histórico de qualidade e não pode ser removido. Inative o app em vez de removê-lo.");
        }

        // Participação em checagem também é histórico (spec 003, FR-014). A FK de
        // check_items.app_id recusaria de qualquer forma; aqui vira 409 com mensagem.
        if (await checks.IsAppInAnyCheckAsync(appId, cancellationToken))
        {
            throw new DomainConflictException(
                $"App '{appId}' está em uma ou mais checagens e não pode ser removido. Inative o app em vez de removê-lo.");
        }

        await apps.DeleteAllAsync(appId, cancellationToken);
    }

    public Task<IReadOnlyList<AppLink>> ListLinksAsync(string appId, CancellationToken cancellationToken = default) =>
        apps.GetLinksAsync(appId, cancellationToken);

    public async Task<AppLink> AddLinkAsync(
        string appId, string description, string uri, CancellationToken cancellationToken = default)
    {
        var link = new AppLink { Id = Guid.NewGuid().ToString("n"), AppId = appId, Description = description, Uri = uri };
        await apps.SaveLinkAsync(link, cancellationToken);
        return link;
    }

    public async Task<AppLink> UpdateLinkAsync(
        string appId, string linkId, string description, string uri, CancellationToken cancellationToken = default)
    {
        var link = new AppLink { Id = linkId, AppId = appId, Description = description, Uri = uri };
        await apps.SaveLinkAsync(link, cancellationToken);
        return link;
    }

    public Task DeleteLinkAsync(string appId, string linkId, CancellationToken cancellationToken = default) =>
        apps.DeleteLinkAsync(appId, linkId, cancellationToken);

    private async Task EnsureSquadExistsAsync(string squadId, CancellationToken cancellationToken)
    {
        if (await squads.GetAsync(squadId, cancellationToken) is null)
        {
            throw new KeyNotFoundException($"Squad '{squadId}' não encontrada.");
        }
    }
}
