// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

namespace DisplayPilot.Display.Rotation;

public enum DisplayRotation
{
    Landscape = 0,
    Portrait = 1,
    LandscapeFlipped = 2,
    PortraitFlipped = 3,
}

public enum DisplayRotationStatus
{
    ReadSucceeded,
    ReadFailed,
    Applied,
    RestartRequired,
    TestFailed,
    ApplyFailed,
}

public sealed record DisplayRotationResult(
    string GdiDeviceName,
    DisplayRotationStatus Status,
    DisplayRotation? Rotation,
    int? NativeResult = null,
    string Message = "")
{
    public bool Succeeded => Status is
        DisplayRotationStatus.ReadSucceeded or
        DisplayRotationStatus.Applied or
        DisplayRotationStatus.RestartRequired;
}
