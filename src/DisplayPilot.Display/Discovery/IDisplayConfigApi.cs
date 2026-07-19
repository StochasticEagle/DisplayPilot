// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace DisplayPilot.Display.Discovery;

/// <summary>
/// Read-only boundary around the Windows DisplayConfig APIs.
/// </summary>
public interface IDisplayConfigApi
{
    IReadOnlyList<DisplayConfigPathDescriptor> QueryActiveDisplayPaths();
}
