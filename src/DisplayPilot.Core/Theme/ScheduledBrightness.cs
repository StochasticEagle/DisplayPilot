// SPDX-License-Identifier: Unlicense
namespace DisplayPilot.Core.Theme;

public static class ScheduledBrightness
{
    public static int CalculateReducedValue(int currentValue)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(currentValue, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(currentValue, 100);
        return currentValue > 10 ? currentValue / 2 : 0;
    }
}
