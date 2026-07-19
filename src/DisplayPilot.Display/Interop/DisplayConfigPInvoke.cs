// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace DisplayPilot.Display.Interop;

internal static partial class DisplayConfigPInvoke
{
    [LibraryImport("user32.dll")]
    internal static partial int GetDisplayConfigBufferSizes(
        uint flags,
        out uint pathCount,
        out uint modeCount);

    [LibraryImport("user32.dll")]
    internal static unsafe partial int QueryDisplayConfig(
        uint flags,
        ref uint pathCount,
        DisplayConfigPathInfo* paths,
        ref uint modeCount,
        DisplayConfigModeInfo* modes,
        nint currentTopologyId);

    [LibraryImport("user32.dll")]
    internal static unsafe partial int DisplayConfigGetDeviceInfo(DisplayConfigSourceDeviceName* sourceName);

    [LibraryImport("user32.dll")]
    internal static unsafe partial int DisplayConfigGetDeviceInfo(DisplayConfigTargetDeviceName* targetName);
}
