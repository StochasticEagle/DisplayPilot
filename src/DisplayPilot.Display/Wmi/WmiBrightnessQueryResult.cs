// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

namespace DisplayPilot.Display.Wmi;

/// <summary>
/// Result of executing the read-only WMI brightness query.
/// </summary>
public sealed record WmiBrightnessQueryResult(
    IReadOnlyList<WmiBrightnessInstance> Instances,
    int ErrorCode = 0,
    string ErrorMessage = "")
{
    public bool Succeeded => ErrorCode == 0;

    public static WmiBrightnessQueryResult Failed(int errorCode, string message) =>
        new([], errorCode, message);
}
