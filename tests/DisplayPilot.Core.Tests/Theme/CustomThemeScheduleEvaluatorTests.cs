// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using DisplayPilot.Core.Theme;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DisplayPilot.Core.Tests.Theme;

[TestClass]
public sealed class CustomThemeScheduleEvaluatorTests
{
    private static readonly CustomThemeSchedule DaySchedule = new(
        new TimeOnly(7, 0),
        new TimeOnly(19, 0));

    [TestMethod]
    public void BeforeLightBoundaryIsDark()
    {
        var result = Evaluate(DaySchedule, 6, 59);

        Assert.AreEqual(ThemeMode.Dark, result.ActiveMode);
        Assert.AreEqual(new TimeOnly(7, 0), result.NextTransitionTime);
        Assert.AreEqual(ThemeMode.Light, result.NextMode);
        Assert.AreEqual(TimeSpan.FromMinutes(1), result.TimeUntilNextTransition);
    }

    [TestMethod]
    public void LightBoundaryIsInclusive()
    {
        var result = Evaluate(DaySchedule, 7, 0);

        Assert.AreEqual(ThemeMode.Light, result.ActiveMode);
        Assert.AreEqual(new TimeOnly(19, 0), result.NextTransitionTime);
        Assert.AreEqual(TimeSpan.FromHours(12), result.TimeUntilNextTransition);
    }

    [TestMethod]
    public void DarkBoundaryIsInclusive()
    {
        var result = Evaluate(DaySchedule, 19, 0);

        Assert.AreEqual(ThemeMode.Dark, result.ActiveMode);
        Assert.AreEqual(new TimeOnly(7, 0), result.NextTransitionTime);
        Assert.AreEqual(TimeSpan.FromHours(12), result.TimeUntilNextTransition);
    }

    [TestMethod]
    public void MidnightWrapScheduleSupportsEveningLightMode()
    {
        var schedule = new CustomThemeSchedule(new TimeOnly(19, 0), new TimeOnly(7, 0));

        var evening = Evaluate(schedule, 23, 0);
        var morning = Evaluate(schedule, 6, 59);
        var daytime = Evaluate(schedule, 12, 0);

        Assert.AreEqual(ThemeMode.Light, evening.ActiveMode);
        Assert.AreEqual(ThemeMode.Light, morning.ActiveMode);
        Assert.AreEqual(ThemeMode.Dark, daytime.ActiveMode);
    }

    [TestMethod]
    public void NextTransitionWrapsToFollowingDay()
    {
        var result = Evaluate(DaySchedule, 23, 30);

        Assert.AreEqual(new TimeOnly(7, 0), result.NextTransitionTime);
        Assert.AreEqual(TimeSpan.FromHours(7.5), result.TimeUntilNextTransition);
    }

    [TestMethod]
    public void EqualTransitionTimesAreRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new CustomThemeSchedule(new TimeOnly(7, 0), new TimeOnly(7, 0)));
    }

    private static ThemeScheduleEvaluation Evaluate(CustomThemeSchedule schedule, int hour, int minute) =>
        new CustomThemeScheduleEvaluator().Evaluate(schedule, new TimeOnly(hour, minute));
}
