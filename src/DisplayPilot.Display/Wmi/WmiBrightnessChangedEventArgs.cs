// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

namespace DisplayPilot.Display.Wmi;

public sealed class WmiBrightnessChangedEventArgs(
    string instanceName,
    byte brightness,
    bool active) : EventArgs
{
    public string InstanceName { get; } = instanceName;

    public byte Brightness { get; } = brightness;

    public bool Active { get; } = active;
}
