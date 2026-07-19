// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace DisplayPilot.Display.Discovery;

/// <summary>
/// Raw names returned for an active QueryDisplayConfig path.
/// </summary>
public readonly record struct DisplayConfigPathDescriptor(
    string? DevicePath,
    string? GdiDeviceName,
    string? FriendlyName,
    int MonitorNumber);
