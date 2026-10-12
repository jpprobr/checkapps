using CheckApps.Domain.Checks;

namespace CheckApps.UnitTests.Checks;

public class CheckCodesTests
{
    [Theory]
    [InlineData(CheckItemStatus.Pendente, "pendente")]
    [InlineData(CheckItemStatus.EmAndamento, "em-andamento")]
    [InlineData(CheckItemStatus.Conforme, "conforme")]
    [InlineData(CheckItemStatus.NaoSeAplica, "nao-se-aplica")]
    [InlineData(CheckItemStatus.Concluido, "concluido")]
    public void ItemStatus_RoundTripsThroughGlossaryCode(CheckItemStatus status, string code)
    {
        Assert.Equal(code, status.ToCode());
        Assert.Equal(status, CheckCodes.ParseItemStatus(code));
    }

    [Fact]
    public void EveryEnumValue_HasACode()
    {
        Assert.All(Enum.GetValues<CheckItemStatus>(), s => Assert.Equal(s, CheckCodes.ParseItemStatus(s.ToCode())));
        Assert.All(Enum.GetValues<CheckStatus>(), s => Assert.Equal(s, CheckCodes.ParseStatus(s.ToCode())));
        Assert.All(Enum.GetValues<CheckEventType>(), t => Assert.Equal(t, CheckCodes.ParseEventType(t.ToCode())));
    }

    [Theory]
    [InlineData("EmAndamento")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParseItemStatus_RejectsUnknownCodes(string? code)
    {
        Assert.False(CheckCodes.TryParseItemStatus(code, out _));
    }
}
