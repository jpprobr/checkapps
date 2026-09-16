using CheckApps.Application.Persistence;
using CheckApps.Domain;

namespace CheckApps.Application.Squads;

public class SquadService(ISquadRepository squads, IAppRepository apps, IQualitySnapshotRepository snapshots)
{
    public Task<IReadOnlyList<Squad>> ListSquadsAsync(CancellationToken cancellationToken = default) =>
        squads.ListAsync(cancellationToken);

    public Task<Squad?> GetSquadAsync(string squadId, CancellationToken cancellationToken = default) =>
        squads.GetAsync(squadId, cancellationToken);

    public Task<IReadOnlyList<App>> ListAppsAsync(string squadId, CancellationToken cancellationToken = default) =>
        apps.ListBySquadAsync(squadId, cancellationToken);

    public async Task<Squad> CreateAsync(string name, string? description, CancellationToken cancellationToken = default)
    {
        Squad? created = null;

        await SlugIdGenerator.GenerateUniqueAsync(name, async (candidateId, ct) =>
        {
            var squad = new Squad { Id = candidateId, Name = name, Description = description, Status = "active" };
            if (!await squads.TryCreateAsync(squad, ct))
            {
                return false;
            }

            created = squad;
            return true;
        }, cancellationToken);

        return created!;
    }

    public async Task<Squad> UpdateAsync(
        string squadId, string name, string? description, string? status, CancellationToken cancellationToken = default)
    {
        var squad = await squads.GetAsync(squadId, cancellationToken)
            ?? throw new KeyNotFoundException($"Squad '{squadId}' não encontrada.");

        squad.Name = name;
        squad.Description = description;
        squad.Status = status;

        await squads.SaveAsync(squad, cancellationToken);
        return squad;
    }

    public async Task DeleteAsync(string squadId, CancellationToken cancellationToken = default)
    {
        var linkedApps = await apps.ListBySquadAsync(squadId, cancellationToken);
        if (linkedApps.Count > 0)
        {
            throw new DomainConflictException(
                $"Squad '{squadId}' possui {linkedApps.Count} app(s) vinculado(s) e não pode ser removida. Mova ou remova os apps primeiro.");
        }

        await squads.DeleteAsync(squadId, cancellationToken);
    }

    /// <summary>
    /// Agrega, em memória, o histórico de todos os apps da squad num período: a
    /// distribuição de Quality Level por data de coleta e a tendência de cada app
    /// (comparando os dois snapshots mais recentes no período). Ver seção 8 do plano:
    /// não há pré-agregação no banco no MVP.
    /// </summary>
    public async Task<SquadQualityEvolution> GetQualityEvolutionAsync(
        string squadId, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default)
    {
        var squadApps = await apps.ListBySquadAsync(squadId, cancellationToken);

        var historiesByApp = new Dictionary<App, IReadOnlyList<QualitySnapshot>>();
        foreach (var app in squadApps)
        {
            historiesByApp[app] = await snapshots.GetHistoryAsync(app.Id, from, to, cancellationToken);
        }

        var points = historiesByApp
            .SelectMany(kv => kv.Value)
            .GroupBy(s => s.CollectedAt)
            .OrderBy(g => g.Key)
            .Select(g => new SquadQualityEvolutionPoint(
                g.Key,
                g.GroupBy(s => s.QualityLevel).ToDictionary(lg => lg.Key, lg => lg.Count())))
            .ToList();

        var trends = historiesByApp
            .Select(kv => BuildTrend(kv.Key, kv.Value))
            .Where(trend => trend is not null)
            .Select(trend => trend!)
            .ToList();

        return new SquadQualityEvolution(points, trends);
    }

    private static AppQualityTrend? BuildTrend(App app, IReadOnlyList<QualitySnapshot> history)
    {
        if (history.Count == 0)
        {
            return null;
        }

        var ordered = history.OrderBy(s => s.CollectedAt).ToList();
        var current = ordered[^1];

        if (ordered.Count == 1)
        {
            return new AppQualityTrend(app.Id, app.Name, null, current.QualityLevel, QualityTrendDirection.New);
        }

        var previous = ordered[^2];
        var direction = current.QualityLevel.CompareTo(previous.QualityLevel) switch
        {
            < 0 => QualityTrendDirection.Improved,
            > 0 => QualityTrendDirection.Worsened,
            _ => QualityTrendDirection.Stable
        };

        return new AppQualityTrend(app.Id, app.Name, previous.QualityLevel, current.QualityLevel, direction);
    }
}
