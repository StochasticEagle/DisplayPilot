// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

namespace DisplayPilot.Display.Wmi;

/// <summary>
/// Reads brightness information exposed by the Windows monitor WMI provider.
/// </summary>
public interface IWmiBrightnessApi
{
    WmiBrightnessQueryResult QueryBrightness();
}
