using Game.Application.Formatters;
using Game.Core.Definitions;

namespace Game.Tests;

public sealed class ItemUseEffectFormatterTests
{
    [Fact]
    public void FormatCn_DescribesBothDetoxifyValues()
    {
        var text = ItemUseEffectFormatter.FormatCn(
            new DetoxifyItemUseEffectDefinition([5, 5]),
            TestContentFactory.CreateRepository());

        Assert.Equal("解毒：降低中毒等级 5，缩短持续时间 5 回合", text);
    }
}
