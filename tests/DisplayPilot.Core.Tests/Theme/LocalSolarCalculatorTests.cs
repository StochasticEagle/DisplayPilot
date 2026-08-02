using DisplayPilot.Core.Theme;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DisplayPilot.Core.Tests.Theme;

[TestClass]
public sealed class LocalSolarCalculatorTests
{
    [TestMethod]
    public void NewYorkSummerSolsticeMatchesPublishedTimesWithinFiveMinutes()
    {
        var result = LocalSolarCalculator.Calculate(
            new DateOnly(2026, 6, 21),
            new SolarLocation(40.7128, -74.0060, "New York"),
            TimeZoneInfo.CreateCustomTimeZone("EDT", TimeSpan.FromHours(-4), "EDT", "EDT"));

        Assert.AreEqual(SolarDayCondition.Normal, result.Condition);
        AssertTimeWithin(result.Sunrise, new TimeOnly(5, 25), 5);
        AssertTimeWithin(result.Sunset, new TimeOnly(20, 31), 5);
    }

    [DataTestMethod]
    [DataRow(69.6492, 18.9553, 2026, 6, 21, SolarDayCondition.PolarDay)]
    [DataRow(69.6492, 18.9553, 2026, 12, 21, SolarDayCondition.PolarNight)]
    [DataRow(-77.8419, 166.6863, 2026, 6, 21, SolarDayCondition.PolarNight)]
    [DataRow(-77.8419, 166.6863, 2026, 12, 21, SolarDayCondition.PolarDay)]
    public void PolarRegionsReportTheExpectedCondition(
        double latitude,
        double longitude,
        int year,
        int month,
        int day,
        SolarDayCondition expected)
    {
        var result = LocalSolarCalculator.Calculate(
            new DateOnly(year, month, day),
            new SolarLocation(latitude, longitude),
            TimeZoneInfo.Utc);

        Assert.AreEqual(expected, result.Condition);
        Assert.IsNull(result.Sunrise);
        Assert.IsNull(result.Sunset);
    }

    [DataTestMethod]
    [DataRow(-90.1, 0d)]
    [DataRow(90.1, 0d)]
    [DataRow(0d, -180.1)]
    [DataRow(0d, 180.1)]
    public void LocationRejectsOutOfRangeCoordinates(double latitude, double longitude)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            Assert.IsNotNull(new SolarLocation(latitude, longitude)));
    }

    private static void AssertTimeWithin(
        DateTimeOffset? actual,
        TimeOnly expected,
        int toleranceMinutes)
    {
        Assert.IsNotNull(actual);
        var actualTime = TimeOnly.FromDateTime(actual.Value.DateTime);
        var directDifference = Math.Abs(
            (actual.Value.TimeOfDay - expected.ToTimeSpan()).TotalMinutes);
        var difference = Math.Min(directDifference, TimeSpan.FromDays(1).TotalMinutes - directDifference);
        Assert.IsTrue(
            difference <= toleranceMinutes,
            $"Expected {expected} ± {toleranceMinutes} minutes but calculated {actualTime}.");
    }
}
