using CheckApps.Domain;

namespace CheckApps.Application.Squads;

public enum QualityTrendDirection
{
    New,
    Improved,
    Worsened,
    Stable
}

public record AppQualityTrend(
    string AppId,
    string AppName,
    QualityLevel? PreviousLevel,
    QualityLevel CurrentLevel,
    QualityTrendDirection Direction);

public record SquadQualityEvolutionPoint(
    DateTimeOffset Period,
    IReadOnlyDictionary<QualityLevel, int> LevelCounts);

public record SquadQualityEvolution(
    IReadOnlyList<SquadQualityEvolutionPoint> Points,
    IReadOnlyList<AppQualityTrend> AppTrends);
