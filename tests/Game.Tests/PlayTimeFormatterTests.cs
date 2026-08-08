using Game.Application;

namespace Game.Tests;

public sealed class PlayTimeFormatterTests
{
    [Theory]
    [InlineData(0, "0分钟")]
    [InlineData(59, "0分钟")]
    [InlineData(60, "1分钟")]
    [InlineData(3599, "59分钟")]
    [InlineData(3600, "1小时0分钟")]
    [InlineData(7380, "2小时3分钟")]
    public void FormatHoursAndMinutes_FormatsCompletedMinutes(long seconds, string expected)
    {
        Assert.Equal(expected, PlayTimeFormatter.FormatHoursAndMinutes(seconds));
    }

    [Fact]
    public void FormatHoursAndMinutes_RejectsNegativeValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayTimeFormatter.FormatHoursAndMinutes(-1));
    }
}
