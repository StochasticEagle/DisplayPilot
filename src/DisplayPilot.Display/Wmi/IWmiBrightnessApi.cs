// SPDX-License-Identifier: Unlicense
namespace DisplayPilot.Display.Wmi;

/// <summary>
/// Reads brightness information exposed by the Windows monitor WMI provider.
/// </summary>
public interface IWmiBrightnessApi
{
    WmiBrightnessQueryResult QueryBrightness();
}
