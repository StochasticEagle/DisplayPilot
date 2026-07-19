// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using System.Management;
using System.Runtime.InteropServices;
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
            using var searcher = new ManagementObjectSearcher(Scope, Query);
            using var results = searcher.Get();
            var instances = new List<WmiBrightnessInstance>(results.Count);

            foreach (ManagementBaseObject result in results)
            {
                using (result)
                {
                    instances.Add(new WmiBrightnessInstance(
                        result["InstanceName"] as string ?? string.Empty,
                        result["Active"] is bool active && active,
                        result["CurrentBrightness"] is byte brightness ? brightness : (byte)0,
                        result["Level"] as byte[] ?? []));
                }
            }

            return new WmiBrightnessQueryResult(instances);
        }
        catch (ManagementException exception)
        {
            return WmiBrightnessQueryResult.Failed(exception.HResult, exception.Message);
        }
        catch (COMException exception)
        {
            return WmiBrightnessQueryResult.Failed(exception.HResult, exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return WmiBrightnessQueryResult.Failed(exception.HResult, exception.Message);
        }
    }
}
