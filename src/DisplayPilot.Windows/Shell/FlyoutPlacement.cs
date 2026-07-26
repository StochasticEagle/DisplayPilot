// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

namespace DisplayPilot.Windows.Shell;

public static class FlyoutPlacement
{
    public static NotificationAreaBounds Calculate(
        NotificationAreaBounds icon,
        NotificationAreaBounds workArea,
        int width,
        int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var x = Clamp(icon.Right - width, workArea.Left, workArea.Right - width);
        var y = Clamp(icon.Top - height, workArea.Top, workArea.Bottom - height);

        if (icon.Bottom > workArea.Bottom)
        {
            y = workArea.Bottom - height;
        }
        else if (icon.Top < workArea.Top)
        {
            y = workArea.Top;
        }
        else if (icon.Right > workArea.Right)
        {
            x = workArea.Right - width;
            y = Clamp(icon.Bottom - height, workArea.Top, workArea.Bottom - height);
        }
        else if (icon.Left < workArea.Left)
        {
            x = workArea.Left;
            y = Clamp(icon.Bottom - height, workArea.Top, workArea.Bottom - height);
        }

        return new NotificationAreaBounds(x, y, x + width, y + height);
    }

    private static int Clamp(int value, int minimum, int maximum)
    {
        if (maximum < minimum)
        {
            return minimum;
        }

        return Math.Clamp(value, minimum, maximum);
    }
}
