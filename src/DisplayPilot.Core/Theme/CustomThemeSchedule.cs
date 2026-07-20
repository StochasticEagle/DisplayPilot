// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

namespace DisplayPilot.Core.Theme;

public sealed record CustomThemeSchedule
{
    public CustomThemeSchedule(TimeOnly lightTime, TimeOnly darkTime)
    {
        if (lightTime == darkTime)
        {
            throw new ArgumentException(
                "Light and dark transition times must be different.",
                nameof(darkTime));
        }

        LightTime = lightTime;
        DarkTime = darkTime;
    }

    public TimeOnly LightTime { get; }

    public TimeOnly DarkTime { get; }
}
