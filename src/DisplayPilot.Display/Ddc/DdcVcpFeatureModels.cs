// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using DisplayPilot.Display.Discovery;
using DisplayPilot.Display.Models;

namespace DisplayPilot.Display.Ddc;

public enum DdcVcpFeatureStatus
{
    NoPhysicalMonitor,
    PhysicalMonitorEnumerationFailed,
    PhysicalMonitorHandleUnavailable,
    ReadSucceeded,
    ReadFailed,
}

public sealed record DdcVcpFeatureResult(
    string GdiDeviceName,
    string PhysicalMonitorDescription,
    byte VcpCode,
    DdcVcpFeatureStatus Status,
    uint CurrentValue,
    uint MaximumValue,
    int Win32Error,
    int AttemptCount = 0,
    int HandleAcquisitionAttempts = 0);

public sealed record MonitorDdcVcpFeatureInfo(
    MonitorDisplayInfo Display,
    IReadOnlyList<DdcVcpFeatureResult> PhysicalMonitors);

public sealed record MonitorDdcCapabilitiesInfo(
    MonitorDisplayInfo Display,
    bool Succeeded,
    string RawCapabilities,
    VcpCapabilities Capabilities,
    int Win32Error = 0);

public enum DdcVcpWriteStatus
{
    WriteSucceeded,
    WriteFailed,
    VerificationFailed,
}

public sealed record DdcVcpWriteResult(
    byte VcpCode,
    DdcVcpWriteStatus Status,
    uint RequestedRawValue,
    uint AppliedRawValue,
    uint? VerifiedRawValue,
    int? RequestedPercent = null,
    int? VerifiedPercent = null,
    int ErrorCode = 0,
    string Message = "")
{
    public bool Succeeded => Status == DdcVcpWriteStatus.WriteSucceeded;
}
