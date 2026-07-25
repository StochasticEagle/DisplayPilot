// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using System.Runtime.InteropServices;

namespace DisplayPilot.Windows.Shell;

public static partial class WindowWorkArea
{
    private const uint DefaultToNearestMonitor = 2;
    private const uint DefaultDpi = 96;

    public static double GetScale(nint window)
    {
        var dpi = GetDpiForWindow(window);
        return dpi == 0 ? 1 : dpi / (double)DefaultDpi;
    }

    public static bool TryGetNearest(
        NotificationAreaBounds bounds,
        out NotificationAreaBounds workArea)
    {
        var nativeBounds = new NativeRect
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Right = bounds.Right,
            Bottom = bounds.Bottom,
        };
        var monitor = MonitorFromRect(in nativeBounds, DefaultToNearestMonitor);
        var info = new MonitorInformation
        {
            CbSize = (uint)Marshal.SizeOf<MonitorInformation>(),
        };
        if (monitor == 0 || !GetMonitorInformation(monitor, ref info))
        {
            workArea = default;
            return false;
        }

        workArea = new NotificationAreaBounds(
            info.WorkArea.Left,
            info.WorkArea.Top,
            info.WorkArea.Right,
            info.WorkArea.Bottom);
        return true;
    }

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromRect(in NativeRect bounds, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInformation(nint monitor, ref MonitorInformation information);

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForWindow(nint window);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInformation
    {
        internal uint CbSize;
        internal NativeRect MonitorArea;
        internal NativeRect WorkArea;
        internal uint Flags;
    }
}
