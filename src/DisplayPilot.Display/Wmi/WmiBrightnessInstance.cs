// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

namespace DisplayPilot.Display.Wmi;

/// <summary>
/// One read-only <c>WmiMonitorBrightness</c> result.
/// </summary>
public sealed record WmiBrightnessInstance(
    string InstanceName,
    bool Active,
    byte CurrentBrightness,
    IReadOnlyList<byte> Levels);
