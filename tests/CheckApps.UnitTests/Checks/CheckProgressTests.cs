using CheckApps.Domain.Checks;

namespace CheckApps.UnitTests.Checks;

public class CheckProgressTests
{
    [Fact]
    public void From_WithNoItems_HasNoPercent()
    {
        var progress = CheckProgress.From([]);

        Assert.Equal(0, progress.Total);
        Assert.Equal(0, progress.Done);
        Assert.Null(progress.Percent);
        Assert.All(Enum.GetValues<CheckItemStatus>(), s => Assert.Equal(0, progress.CountsByStatus[s]));
    }

    [Fact]
    public void From_CountsConformeNaoSeAplicaAndConcluidoAsDone()
    {
        // Cenário de aceite 3 da User Story 2: 1 conforme, 1 não se aplica, 1 em andamento, 1 pendente → 50%.
        var progress = CheckProgress.From(
        [
            CheckItemStatus.Conforme,
            CheckItemStatus.NaoSeAplica,
            CheckItemStatus.EmAndamento,
            CheckItemStatus.Pendente
        ]);

        Assert.Equal(4, progress.Total);
        Assert.Equal(2, progress.Done);
        Assert.Equal(50.0, progress.Percent);
        Assert.Equal(1, progress.CountsByStatus[CheckItemStatus.EmAndamento]);
        Assert.Equal(0, progress.CountsByStatus[CheckItemStatus.Concluido]);
    }

    [Fact]
    public void From_RoundsPercentToOneDecimal()
    {
        var progress = CheckProgress.From([CheckItemStatus.Concluido, CheckItemStatus.Pendente, CheckItemStatus.Pendente]);

        Assert.Equal(33.3, progress.Percent);
    }

    [Fact]
    public void From_Counts_FillsMissingStatusesWithZero()
    {
        var progress = CheckProgress.From(new Dictionary<CheckItemStatus, int> { [CheckItemStatus.Concluido] = 3 });

        Assert.Equal(3, progress.Total);
        Assert.Equal(100.0, progress.Percent);
        Assert.Equal(0, progress.CountsByStatus[CheckItemStatus.Pendente]);
    }
}
