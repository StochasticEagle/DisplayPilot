namespace DisplayPilot.Core.Theme;

public sealed class CustomThemeScheduleEvaluator
{
    public static ThemeScheduleEvaluation Evaluate(CustomThemeSchedule schedule, TimeOnly now)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        var isLight = schedule.LightTime < schedule.DarkTime
            ? now >= schedule.LightTime && now < schedule.DarkTime
            : now >= schedule.LightTime || now < schedule.DarkTime;
        var nextTransition = isLight ? schedule.DarkTime : schedule.LightTime;
        var timeUntilNextTransition = nextTransition.ToTimeSpan() - now.ToTimeSpan();
        if (timeUntilNextTransition <= TimeSpan.Zero)
        {
            timeUntilNextTransition += TimeSpan.FromDays(1);
        }

        return new ThemeScheduleEvaluation(
            isLight ? ThemeMode.Light : ThemeMode.Dark,
            nextTransition,
            isLight ? ThemeMode.Dark : ThemeMode.Light,
            timeUntilNextTransition);
    }
}
