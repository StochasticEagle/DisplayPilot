// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using DisplayPilot.Display.Discovery;

namespace DisplayPilot.Display.Wmi;

public sealed record WmiBrightnessProbeResult(
    MonitorDisplayInfo Display,
    WmiBrightnessProbeStatus Status,
    string InstanceName,
    byte CurrentBrightness,
    int LevelCount,
    int ErrorCode = 0,
    string ErrorMessage = "");
