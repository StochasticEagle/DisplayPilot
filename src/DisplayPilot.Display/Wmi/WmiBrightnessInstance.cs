// SPDX-License-Identifier: Unlicense
namespace DisplayPilot.Display.Wmi;

/// <summary>
/// One read-only <c>WmiMonitorBrightness</c> result.
/// </summary>
public sealed record WmiBrightnessInstance(
    string InstanceName,
    bool Active,
    byte CurrentBrightness,
    IReadOnlyList<byte> Levels);
