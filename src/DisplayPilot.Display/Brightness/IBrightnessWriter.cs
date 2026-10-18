// SPDX-License-Identifier: Unlicense
using DisplayPilot.Display.Discovery;

namespace DisplayPilot.Display.Brightness;

public interface IBrightnessWriter
{
    BrightnessWriteResult WriteBrightness(MonitorDisplayInfo display, int requestedPercent);
}
