using CheckApps.Application.Persistence;
using CheckApps.Domain;

namespace CheckApps.Application.Squads;

public class SquadService(
    ISquadRepository squads, IAppRepository apps, IQualitySnapshotRepository snapshots, IJourneyRepository journeys)
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

        // Squad dona de jornada (spec 022): a FK recusaria, e aqui vira 409 com mensagem.
        var ownedJourneys = await journeys.CountOwnedBySquadAsync(squadId, cancellationToken);
        if (ownedJourneys > 0)
        {
            throw new DomainConflictException(
                $"Squad '{squadId}' é dona de {ownedJourneys} jornada(s) e não pode ser removida. Troque a squad dessas jornadas primeiro.");
        }

        await squads.DeleteAsync(squadId, cancellationToken);
    }

    /// <summary>
    /// Distribuição de Quality Level por data de coleta e tendência de cada app (comparando
    /// os dois snapshots mais recentes no período), para toda a squad.
    /// <para>
    /// São duas consultas — apps e histórico da squad — e a agregação acontece aqui, não no SQL.
    /// É deliberado: a classificação de tendência (melhorou/piorou/estável/novo) é regra de
    /// negócio e precisa das linhas de qualquer forma, então empurrá-la para o banco só moveria
    /// lógica de lugar. Não há pré-agregação armazenada (ADR-002, mantido pelo ADR-009).
    /// </para>
    /// </summary>
    public async Task<SquadQualityEvolution> GetQualityEvolutionAsync(
        string squadId, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default)
    {
        var squadApps = await apps.ListBySquadAsync(squadId, cancellationToken);
        var squadHistory = await snapshots.GetHistoryBySquadAsync(squadId, from, to, cancellationToken);

        var historyByAppId = squadHistory
            .GroupBy(s => s.AppId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<QualitySnapshot>)g.ToList());

        var points = squadHistory
            .GroupBy(s => s.CollectedAt)
            .OrderBy(g => g.Key)
            .Select(g => new SquadQualityEvolutionPoint(
                g.Key,
                g.GroupBy(s => s.QualityLevel).ToDictionary(lg => lg.Key, lg => lg.Count())))
            .ToList();

        var trends = squadApps
            .Select(app => BuildTrend(app, historyByAppId.GetValueOrDefault(app.Id, [])))
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
