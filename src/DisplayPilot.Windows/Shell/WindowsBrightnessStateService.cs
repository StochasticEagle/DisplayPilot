// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using System.Runtime.InteropServices;

namespace DisplayPilot.Windows.Shell;

/// <summary>
/// Synchronizes the active Windows power/display brightness value used by the
/// shell brightness UI. The value is global even when monitors are controlled
/// independently through DDC/CI.
/// </summary>
public sealed partial class WindowsBrightnessStateService
{
    private static readonly Guid VideoSubgroup =
        new("7516b95f-f776-4464-8c53-06167f40cc99");
    private static readonly Guid VideoBrightnessSetting =
        new("aded5e82-b909-4619-9949-f5d71dac0bcb");

    public WindowsBrightnessStateResult SetCurrentBrightness(int brightnessPercent)
    {
        brightnessPercent = Math.Clamp(brightnessPercent, 0, 100);
        var activeSchemeResult = PowerGetActiveScheme(0, out var schemePointer);
        if (activeSchemeResult != 0 || schemePointer == 0)
        {
            return new WindowsBrightnessStateResult(false, brightnessPercent, activeSchemeResult);
        }

        try
        {
            var scheme = Marshal.PtrToStructure<Guid>(schemePointer);
            var sourceResult = GetSystemPowerStatus(out var powerStatus)
                ? 0u
                : unchecked((uint)Marshal.GetLastPInvokeError());
            if (sourceResult != 0)
            {
                return new WindowsBrightnessStateResult(false, brightnessPercent, sourceResult);
            }

            var writeResult = powerStatus.AcLineStatus == 0
                ? PowerWriteDcValueIndex(
                    0,
                    in scheme,
                    in VideoSubgroup,
                    in VideoBrightnessSetting,
                    (uint)brightnessPercent)
                : PowerWriteAcValueIndex(
                    0,
                    in scheme,
                    in VideoSubgroup,
                    in VideoBrightnessSetting,
                    (uint)brightnessPercent);
            if (writeResult != 0)
            {
                return new WindowsBrightnessStateResult(false, brightnessPercent, writeResult);
            }

            var applyResult = PowerSetActiveScheme(0, in scheme);
            return new WindowsBrightnessStateResult(
                applyResult == 0,
                brightnessPercent,
                applyResult);
        }
        finally
        {
            _ = LocalFree(schemePointer);
        }
    }

    [LibraryImport("powrprof.dll")]
    private static partial uint PowerGetActiveScheme(nint userRootPowerKey, out nint activePolicyGuid);

    [LibraryImport("powrprof.dll")]
    private static partial uint PowerWriteAcValueIndex(
        nint rootPowerKey,
        in Guid schemeGuid,
        in Guid subgroupGuid,
        in Guid settingGuid,
        uint valueIndex);

    [LibraryImport("powrprof.dll")]
    private static partial uint PowerWriteDcValueIndex(
        nint rootPowerKey,
        in Guid schemeGuid,
        in Guid subgroupGuid,
        in Guid settingGuid,
        uint valueIndex);

    [LibraryImport("powrprof.dll")]
    private static partial uint PowerSetActiveScheme(nint userRootPowerKey, in Guid schemeGuid);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemPowerStatus(out SystemPowerStatus systemPowerStatus);

    [LibraryImport("kernel32.dll")]
    private static partial nint LocalFree(nint memory);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        internal byte AcLineStatus;
        internal byte BatteryFlag;
        internal byte BatteryLifePercent;
        internal byte SystemStatusFlag;
        internal uint BatteryLifeTime;
        internal uint BatteryFullLifeTime;
    }
}

public readonly record struct WindowsBrightnessStateResult(
    bool Succeeded,
    int BrightnessPercent,
    uint ErrorCode);
