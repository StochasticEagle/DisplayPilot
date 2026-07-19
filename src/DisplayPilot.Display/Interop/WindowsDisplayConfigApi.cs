// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using DisplayPilot.Display.Discovery;

namespace DisplayPilot.Display.Interop;

/// <summary>
/// Read-only Windows implementation of active display-path enumeration.
/// </summary>
public sealed class WindowsDisplayConfigApi : IDisplayConfigApi
{
    private const int ErrorSuccess = 0;
    private const int ErrorInsufficientBuffer = 122;
    private const int MaximumAttempts = 3;

    public unsafe IReadOnlyList<DisplayConfigPathDescriptor> QueryActiveDisplayPaths()
    {
        for (var attempt = 0; attempt < MaximumAttempts; attempt++)
        {
            var result = DisplayConfigPInvoke.GetDisplayConfigBufferSizes(
                NativeConstants.QdcOnlyActivePaths,
                out var pathCount,
                out var modeCount);

            ThrowIfFailed(result);

            var paths = new DisplayConfigPathInfo[pathCount];
            var modes = new DisplayConfigModeInfo[modeCount];

            fixed (DisplayConfigPathInfo* pathsPointer = paths)
            fixed (DisplayConfigModeInfo* modesPointer = modes)
            {
                result = DisplayConfigPInvoke.QueryDisplayConfig(
                    NativeConstants.QdcOnlyActivePaths,
                    ref pathCount,
                    pathsPointer,
                    ref modeCount,
                    modesPointer,
                    0);
            }

            if (result == ErrorInsufficientBuffer)
            {
                continue;
            }

            ThrowIfFailed(result);

            var displays = new List<DisplayConfigPathDescriptor>((int)pathCount);
            for (var index = 0; index < pathCount; index++)
            {
                var path = paths[index];
                var gdiDeviceName = TryGetSourceName(path.SourceInfo.AdapterId, path.SourceInfo.Id);
                var target = TryGetTargetName(path.TargetInfo.AdapterId, path.TargetInfo.Id);

                displays.Add(new DisplayConfigPathDescriptor(
                    target.DevicePath,
                    gdiDeviceName,
                    target.FriendlyName,
                    (int)index + 1));
            }

            return displays;
        }

        throw new Win32Exception(ErrorInsufficientBuffer, "The active display topology changed repeatedly during discovery.");
    }

    private static unsafe string? TryGetSourceName(Luid adapterId, uint sourceId)
    {
        var sourceName = new DisplayConfigSourceDeviceName
        {
            Header = new DisplayConfigDeviceInfoHeader
            {
                Type = NativeConstants.DisplayconfigDeviceInfoGetSourceName,
                Size = (uint)sizeof(DisplayConfigSourceDeviceName),
                AdapterId = adapterId,
                Id = sourceId,
            },
        };

        var result = DisplayConfigPInvoke.DisplayConfigGetDeviceInfo(&sourceName);
        return result == ErrorSuccess ? sourceName.GetViewGdiDeviceName() : null;
    }

    private static unsafe (string? FriendlyName, string? DevicePath) TryGetTargetName(Luid adapterId, uint targetId)
    {
        var targetName = new DisplayConfigTargetDeviceName
        {
            Header = new DisplayConfigDeviceInfoHeader
            {
                Type = NativeConstants.DisplayconfigDeviceInfoGetTargetName,
                Size = (uint)sizeof(DisplayConfigTargetDeviceName),
                AdapterId = adapterId,
                Id = targetId,
            },
        };

        var result = DisplayConfigPInvoke.DisplayConfigGetDeviceInfo(&targetName);
        return result == ErrorSuccess
            ? (targetName.GetMonitorFriendlyDeviceName(), targetName.GetMonitorDevicePath())
            : (null, null);
    }

    private static void ThrowIfFailed(int errorCode)
    {
        if (errorCode != ErrorSuccess)
        {
            throw new Win32Exception(errorCode);
        }
    }
}
