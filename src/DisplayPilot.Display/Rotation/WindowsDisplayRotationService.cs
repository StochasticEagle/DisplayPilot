// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using System.Runtime.InteropServices;
using DisplayPilot.Display.Interop;

namespace DisplayPilot.Display.Rotation;

public static class WindowsDisplayRotationService
{
    public static DisplayRotationResult Read(string gdiDeviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gdiDeviceName);
        var mode = CreateMode();
        if (!DisplaySettingsPInvoke.EnumDisplaySettings(
                gdiDeviceName,
                NativeConstants.EnumCurrentSettings,
                ref mode))
        {
            return new DisplayRotationResult(
                gdiDeviceName,
                DisplayRotationStatus.ReadFailed,
                null,
                Marshal.GetLastPInvokeError(),
                "EnumDisplaySettingsW could not read the current display mode.");
        }

        return Enum.IsDefined(typeof(DisplayRotation), checked((int)mode.DisplayOrientation))
            ? new DisplayRotationResult(
                gdiDeviceName,
                DisplayRotationStatus.ReadSucceeded,
                (DisplayRotation)mode.DisplayOrientation)
            : new DisplayRotationResult(
                gdiDeviceName,
                DisplayRotationStatus.ReadFailed,
                null,
                Message: $"Windows returned unknown orientation {mode.DisplayOrientation}.");
    }

    public static DisplayRotationResult Apply(
        string gdiDeviceName,
        DisplayRotation requestedRotation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gdiDeviceName);
        if (!Enum.IsDefined(requestedRotation))
        {
            throw new ArgumentOutOfRangeException(nameof(requestedRotation));
        }

        var mode = CreateMode();
        if (!DisplaySettingsPInvoke.EnumDisplaySettings(
                gdiDeviceName,
                NativeConstants.EnumCurrentSettings,
                ref mode))
        {
            return new DisplayRotationResult(
                gdiDeviceName,
                DisplayRotationStatus.ReadFailed,
                null,
                Marshal.GetLastPInvokeError(),
                "The current display mode could not be read.");
        }

        var currentRotation = (DisplayRotation)mode.DisplayOrientation;
        if (RequiresDimensionSwap(currentRotation, requestedRotation))
        {
            (mode.PelsWidth, mode.PelsHeight) = (mode.PelsHeight, mode.PelsWidth);
        }

        mode.DisplayOrientation = (uint)requestedRotation;
        mode.Fields = unchecked((uint)(
            NativeConstants.DmDisplayOrientation |
            NativeConstants.DmPelsWidth |
            NativeConstants.DmPelsHeight));

        var testResult = DisplaySettingsPInvoke.ChangeDisplaySettingsEx(
            gdiDeviceName,
            ref mode,
            0,
            NativeConstants.CdsTest,
            0);
        if (testResult != NativeConstants.DispChangeSuccessful)
        {
            return new DisplayRotationResult(
                gdiDeviceName,
                DisplayRotationStatus.TestFailed,
                requestedRotation,
                testResult,
                "Windows rejected the requested orientation during mode validation.");
        }

        var applyResult = DisplaySettingsPInvoke.ChangeDisplaySettingsEx(
            gdiDeviceName,
            ref mode,
            0,
            NativeConstants.CdsUpdateRegistry,
            0);
        return applyResult switch
        {
            NativeConstants.DispChangeSuccessful => new DisplayRotationResult(
                gdiDeviceName,
                DisplayRotationStatus.Applied,
                requestedRotation),
            NativeConstants.DispChangeRestart => new DisplayRotationResult(
                gdiDeviceName,
                DisplayRotationStatus.RestartRequired,
                requestedRotation,
                applyResult,
                "Windows saved the orientation but requires a restart."),
            _ => new DisplayRotationResult(
                gdiDeviceName,
                DisplayRotationStatus.ApplyFailed,
                requestedRotation,
                applyResult,
                "Windows could not apply the validated orientation."),
        };
    }

    public static bool RequiresDimensionSwap(
        DisplayRotation currentRotation,
        DisplayRotation requestedRotation) =>
        ((int)currentRotation & 1) != ((int)requestedRotation & 1);

    private static unsafe DevMode CreateMode() => new()
    {
        Size = checked((ushort)sizeof(DevMode)),
    };
}
