namespace Game.Application;

public static class PlayTimeFormatter
{
    public static string FormatHoursAndMinutes(long totalSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalSeconds);

        var totalMinutes = totalSeconds / 60;
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        return hours == 0
            ? $"{minutes}分钟"
            : $"{hours}小时{minutes}分钟";
    }
}
