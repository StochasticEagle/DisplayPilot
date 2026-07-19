// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using DisplayPilot.Display.Brightness;
using DisplayPilot.Display.Ddc;
using DisplayPilot.Display.Discovery;
using DisplayPilot.Display.Wmi;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DisplayPilot.Display.Tests.Brightness;

[TestClass]
public sealed class BrightnessControlServiceTests
{
    [TestMethod]
    public void SetBrightnessPrefersValidatedWmiPath()
    {
        var display = Display();
        var ddc = DdcProbe(display, DdcBrightnessProbeStatus.ReadSucceeded);
        var wmi = WmiProbe(display, WmiBrightnessProbeStatus.ReadSucceeded);
        var ddcWriter = new StubWriter(BrightnessWriteProvider.DdcCi);
        var wmiWriter = new StubWriter(BrightnessWriteProvider.Wmi);

        var result = new BrightnessControlService(ddcWriter, wmiWriter)
            .SetBrightness(display, ddc, wmi, 65);

        Assert.AreEqual(BrightnessWriteProvider.Wmi, result.Provider);
        Assert.AreEqual(0, ddcWriter.CallCount);
        Assert.AreEqual(1, wmiWriter.CallCount);
    }

    [TestMethod]
    public void SetBrightnessUsesValidatedDdcPathWhenWmiIsUnavailable()
    {
        var display = Display();
        var ddcWriter = new StubWriter(BrightnessWriteProvider.DdcCi);
        var wmiWriter = new StubWriter(BrightnessWriteProvider.Wmi);

        var result = new BrightnessControlService(ddcWriter, wmiWriter).SetBrightness(
            display,
            DdcProbe(display, DdcBrightnessProbeStatus.ReadSucceeded),
            WmiProbe(display, WmiBrightnessProbeStatus.NotAvailable),
            40);

        Assert.AreEqual(BrightnessWriteProvider.DdcCi, result.Provider);
        Assert.AreEqual(1, ddcWriter.CallCount);
        Assert.AreEqual(0, wmiWriter.CallCount);
    }

    [TestMethod]
    public void SetBrightnessDoesNotWriteWithoutValidatedReadPath()
    {
        var display = Display();
        var ddcWriter = new StubWriter(BrightnessWriteProvider.DdcCi);
        var wmiWriter = new StubWriter(BrightnessWriteProvider.Wmi);

        var result = new BrightnessControlService(ddcWriter, wmiWriter).SetBrightness(
            display,
            DdcProbe(display, DdcBrightnessProbeStatus.ReadFailed),
            WmiProbe(display, WmiBrightnessProbeStatus.NotAvailable),
            50);

        Assert.AreEqual(BrightnessWriteStatus.Unsupported, result.Status);
        Assert.AreEqual(0, ddcWriter.CallCount);
        Assert.AreEqual(0, wmiWriter.CallCount);
    }

    [TestMethod]
    public void SetBrightnessClampsPercentageBeforeCallingWriter()
    {
        var display = Display();
        var writer = new StubWriter(BrightnessWriteProvider.Wmi);

        _ = new BrightnessControlService(new StubWriter(BrightnessWriteProvider.DdcCi), writer)
            .SetBrightness(
                display,
                DdcProbe(display, DdcBrightnessProbeStatus.ReadFailed),
                WmiProbe(display, WmiBrightnessProbeStatus.ReadSucceeded),
                150);

        Assert.AreEqual(100, writer.LastRequestedPercent);
    }

    private static MonitorDisplayInfo Display() =>
        new(@"\\?\DISPLAY#BOE0900#4&abc&0&UID111#{guid}", @"\\.\DISPLAY1", "Panel", 1);

    private static MonitorDdcProbeInfo DdcProbe(
        MonitorDisplayInfo display,
        DdcBrightnessProbeStatus status) =>
        new(display, [new(display.GdiDeviceName, "Monitor", status, 50, 100, 0)]);

    private static WmiBrightnessProbeResult WmiProbe(
        MonitorDisplayInfo display,
        WmiBrightnessProbeStatus status) =>
        new(display, status, @"DISPLAY\BOE0900\4&abc&0&UID111_0", 50, 101);

    private sealed class StubWriter(BrightnessWriteProvider provider) : IBrightnessWriter
    {
        public int CallCount { get; private set; }

        public int LastRequestedPercent { get; private set; } = -1;

        public BrightnessWriteResult WriteBrightness(MonitorDisplayInfo display, int requestedPercent)
        {
            CallCount++;
            LastRequestedPercent = requestedPercent;
            return new BrightnessWriteResult(
                provider,
                BrightnessWriteStatus.WriteSucceeded,
                requestedPercent,
                requestedPercent,
                requestedPercent);
        }
    }
}
