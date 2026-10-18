// SPDX-License-Identifier: Unlicense
using WmiLight;
using DisplayPilot.Display.Wmi;

namespace DisplayPilot.Display.Interop;

/// <summary>
/// Queries the read-only <c>WmiMonitorBrightness</c> class in <c>root\WMI</c>.
/// </summary>
public sealed class WindowsWmiBrightnessApi : IWmiBrightnessApi
{
    private const string Scope = @"root\WMI";
    private const string Query =
        "SELECT Active, CurrentBrightness, InstanceName, Level FROM WmiMonitorBrightness";

    public WmiBrightnessQueryResult QueryBrightness()
    {
        try
        {
            using var connection = new WmiConnection(Scope);
            var instances = new List<WmiBrightnessInstance>();

            foreach (var result in connection.CreateQuery(Query))
            {
                using (result)
                {
                    instances.Add(new WmiBrightnessInstance(
                        result.GetPropertyValue<string>("InstanceName") ?? string.Empty,
                        result.GetPropertyValue<bool>("Active"),
                        result.GetPropertyValue<byte>("CurrentBrightness"),
                        result.GetPropertyValue<byte[]>("Level") ?? []));
                }
            }

            return new WmiBrightnessQueryResult(instances);
        }
        catch (COMException exception)
        {
            return WmiBrightnessQueryResult.Failed(exception.HResult, exception.Message);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return WmiBrightnessQueryResult.Failed(exception.HResult, exception.Message);
        }
    }
}
