using Game.Core.Battle;
using Game.Presentation.Battle;

namespace Game.Tests.Battle;

public sealed class BattleCommandFailurePresenterTests
{
    [Fact]
    public void Format_CoversEveryFailureReason()
    {
        foreach (var reason in Enum.GetValues<BattleCommandFailureReason>())
        {
            int? remainingTurns = reason == BattleCommandFailureReason.ItemOnCooldown ? 1 : null;
            var failure = new BattleCommandFailure(reason, remainingTurns);

            Assert.False(string.IsNullOrWhiteSpace(BattleCommandFailurePresenter.Format(failure)));
        }
    }

    [Theory]
    [InlineData(BattleCommandFailureReason.NotEnoughMp, "内力不足。")]
    [InlineData(BattleCommandFailureReason.DestinationUnreachable, "无法移动到该位置。")]
    [InlineData(BattleCommandFailureReason.SkillCannotTargetSelf, "该技能不能对自己施展。")]
    public void Format_ReturnsPlayerFacingMessage(
        BattleCommandFailureReason reason,
        string expected)
    {
        var failure = new BattleCommandFailure(reason);

        Assert.Equal(expected, BattleCommandFailurePresenter.Format(failure));
    }

    [Fact]
    public void Format_IncludesRemainingItemCooldownTurns()
    {
        var failure = new BattleCommandFailure(
            BattleCommandFailureReason.ItemOnCooldown,
            RemainingTurns: 2);

        Assert.Equal(
            "还需等待 2 回合才能再次使用物品。",
            BattleCommandFailurePresenter.Format(failure));
    }
}
