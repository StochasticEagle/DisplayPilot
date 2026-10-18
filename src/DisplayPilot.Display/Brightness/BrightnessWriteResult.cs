// SPDX-License-Identifier: Unlicense
namespace DisplayPilot.Display.Brightness;

public sealed record BrightnessWriteResult(
    BrightnessWriteProvider Provider,
    BrightnessWriteStatus Status,
    int RequestedPercent,
    int AppliedPercent,
    int VerifiedPercent,
    int ErrorCode = 0,
    string Message = "")
{
    public bool Succeeded => Status == BrightnessWriteStatus.WriteSucceeded;

    public static BrightnessWriteResult Unsupported(int requestedPercent) =>
        new(BrightnessWriteProvider.None, BrightnessWriteStatus.Unsupported, requestedPercent, -1, -1);
}
