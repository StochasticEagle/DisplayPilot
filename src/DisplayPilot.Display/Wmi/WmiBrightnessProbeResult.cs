// SPDX-License-Identifier: Unlicense
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
