namespace CheckApps.Domain.Checks;

/// <summary>
/// Percentual e contagem por status de uma checagem (FR-007). Contam como concluídos:
/// conforme, não se aplica e concluído. Com zero apps não há percentual (null), e a
/// tela mostra "—" em vez de 0%.
/// </summary>
public record CheckProgress(int Total, int Done, double? Percent, IReadOnlyDictionary<CheckItemStatus, int> CountsByStatus)
{
    public static bool CountsAsDone(CheckItemStatus status) =>
        status is CheckItemStatus.Conforme or CheckItemStatus.NaoSeAplica or CheckItemStatus.Concluido;

    public static CheckProgress From(IEnumerable<CheckItemStatus> statuses)
    {
        var counts = Enum.GetValues<CheckItemStatus>().ToDictionary(s => s, _ => 0);
        foreach (var status in statuses)
        {
            counts[status]++;
        }

        return From(counts);
    }

    public static CheckProgress From(IReadOnlyDictionary<CheckItemStatus, int> countsByStatus)
    {
        var counts = Enum.GetValues<CheckItemStatus>().ToDictionary(s => s, s => countsByStatus.GetValueOrDefault(s));
        var total = counts.Values.Sum();
        var done = counts.Where(c => CountsAsDone(c.Key)).Sum(c => c.Value);
        double? percent = total == 0 ? null : Math.Round(100.0 * done / total, 1);

        return new CheckProgress(total, done, percent, counts);
    }
}
