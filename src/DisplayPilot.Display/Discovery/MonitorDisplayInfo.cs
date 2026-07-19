// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace DisplayPilot.Display.Discovery;

/// <summary>
/// Identifies one active Windows display path without opening the physical monitor.
/// </summary>
public sealed record MonitorDisplayInfo(
    string DevicePath,
    string GdiDeviceName,
    string FriendlyName,
    int MonitorNumber);
