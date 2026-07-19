// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using DisplayPilot.Display.Discovery;

namespace DisplayPilot.Display.Brightness;

public interface IBrightnessWriter
{
    BrightnessWriteResult WriteBrightness(MonitorDisplayInfo display, int requestedPercent);
}
