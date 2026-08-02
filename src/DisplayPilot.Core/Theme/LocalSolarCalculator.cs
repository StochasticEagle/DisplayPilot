// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

namespace DisplayPilot.Core.Theme;

public enum SolarDayCondition
{
    Normal,
    PolarDay,
    PolarNight,
}

public sealed record SolarTimes(
    DateOnly Date,
    SolarDayCondition Condition,
    DateTimeOffset? Sunrise,
    DateTimeOffset? Sunset);

public static class LocalSolarCalculator
{
    private const double OfficialZenithDegrees = 90.833;

    public static SolarTimes Calculate(
        DateOnly date,
        SolarLocation location,
        TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(timeZone);

        var daysInYear = DateTime.IsLeapYear(date.Year) ? 366d : 365d;
        var gamma = 2d * Math.PI / daysInYear * (date.DayOfYear - 1);
        var equationOfTime = 229.18d *
            (0.000075d +
             (0.001868d * Math.Cos(gamma)) -
             (0.032077d * Math.Sin(gamma)) -
             (0.014615d * Math.Cos(2d * gamma)) -
             (0.040849d * Math.Sin(2d * gamma)));
        var declination =
            0.006918d -
            (0.399912d * Math.Cos(gamma)) +
            (0.070257d * Math.Sin(gamma)) -
            (0.006758d * Math.Cos(2d * gamma)) +
            (0.000907d * Math.Sin(2d * gamma)) -
            (0.002697d * Math.Cos(3d * gamma)) +
            (0.00148d * Math.Sin(3d * gamma));

        var latitudeRadians = DegreesToRadians(location.Latitude);
        var zenithRadians = DegreesToRadians(OfficialZenithDegrees);
        var hourAngleCosine =
            (Math.Cos(zenithRadians) /
             (Math.Cos(latitudeRadians) * Math.Cos(declination))) -
            (Math.Tan(latitudeRadians) * Math.Tan(declination));
        if (hourAngleCosine < -1d)
        {
            return new SolarTimes(date, SolarDayCondition.PolarDay, null, null);
        }

        if (hourAngleCosine > 1d)
        {
            return new SolarTimes(date, SolarDayCondition.PolarNight, null, null);
        }

        var localNoon = date.ToDateTime(new TimeOnly(12, 0));
        var utcOffsetMinutes = timeZone.GetUtcOffset(localNoon).TotalMinutes;
        var solarNoonMinutes =
            720d - (4d * location.Longitude) - equationOfTime + utcOffsetMinutes;
        var hourAngleDegrees = RadiansToDegrees(Math.Acos(hourAngleCosine));
        var sunrise = CreateLocalTime(
            date,
            solarNoonMinutes - (4d * hourAngleDegrees),
            timeZone);
        var sunset = CreateLocalTime(
            date,
            solarNoonMinutes + (4d * hourAngleDegrees),
            timeZone);
        return new SolarTimes(date, SolarDayCondition.Normal, sunrise, sunset);
    }

    private static DateTimeOffset CreateLocalTime(
        DateOnly date,
        double minutesAfterMidnight,
        TimeZoneInfo timeZone)
    {
        var localTime = date.ToDateTime(TimeOnly.MinValue).AddMinutes(minutesAfterMidnight);
        return new DateTimeOffset(localTime, timeZone.GetUtcOffset(localTime));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;

    private static double RadiansToDegrees(double radians) => radians * 180d / Math.PI;
}
