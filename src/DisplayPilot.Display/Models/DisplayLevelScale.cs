// SPDX-License-Identifier: Unlicense
namespace DisplayPilot.Display.Models;

// Stateless conversion between monitor-native values and UI percentages.
public static class DisplayLevelScale
{
    public static int ToPercent(int current, int maximum, int minimum = 0)
    {
        if (maximum <= minimum || current < minimum || current > maximum)
            return 0;

        return (int)Math.Round((current - (double)minimum) * 100 / (maximum - minimum));
    }

    public static int FromPercent(int percent, int maximum, int minimum = 0)
    {
        if (maximum <= minimum)
            return minimum;

        return minimum + (int)Math.Round(
            Math.Clamp(percent, 0, 100) * (double)(maximum - minimum) / 100);
    }
}
