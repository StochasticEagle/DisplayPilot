using System;

namespace DisplayPilot.Display.Interfaces;

/// <summary>
/// Complete monitor-control contract used by the application layer.
/// </summary>
public interface IMonitorController : IBasicMonitorController, IDdcController, IDisposable
{
}
