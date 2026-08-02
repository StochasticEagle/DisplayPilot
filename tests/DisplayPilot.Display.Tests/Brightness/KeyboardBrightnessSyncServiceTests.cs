// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using DisplayPilot.Display.Brightness;
using DisplayPilot.Display.Ddc;
using DisplayPilot.Display.Discovery;
using DisplayPilot.Display.Wmi;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DisplayPilot.Display.Tests.Brightness;

[TestClass]
public sealed class KeyboardBrightnessSyncServiceTests
{
    [TestMethod]
    public void SynchronizeWritesAbsolutePercentToEveryValidatedExternalDisplay()
    {
        var panel = Display("PANEL", @"\\.\DISPLAY1");
        var externalOne = Display("EXT1", @"\\.\DISPLAY2");
        var externalTwo = Display("EXT2", @"\\.\DISPLAY3");
        var writer = new StubWriter();

        var results = new KeyboardBrightnessSyncService(writer).Synchronize(
            65,
            [DdcProbe(panel, true), DdcProbe(externalOne, true), DdcProbe(externalTwo, true)],
            [WmiProbe(panel, WmiBrightnessProbeStatus.ReadSucceeded),
                WmiProbe(externalOne, WmiBrightnessProbeStatus.NotAvailable),
                WmiProbe(externalTwo, WmiBrightnessProbeStatus.NotAvailable)]);

        Assert.AreEqual(2, results.Count);
        CollectionAssert.AreEquivalent(
            new[] { externalOne.DevicePath, externalTwo.DevicePath },
            writer.DevicePaths.ToArray());
        Assert.IsTrue(writer.RequestedPercents.All(percent => percent == 65));
    }

    [TestMethod]
    public void SynchronizeDoesNotWriteIntegratedOrUnvalidatedDisplays()
    {
        var panel = Display("PANEL", @"\\.\DISPLAY1");
        var unavailable = Display("EXT", @"\\.\DISPLAY2");
        var writer = new StubWriter();

        var results = new KeyboardBrightnessSyncService(writer).Synchronize(
            40,
            [DdcProbe(panel, true), DdcProbe(unavailable, false)],
            [WmiProbe(panel, WmiBrightnessProbeStatus.ReadSucceeded),
                WmiProbe(unavailable, WmiBrightnessProbeStatus.NotAvailable)]);

        Assert.AreEqual(0, results.Count);
        Assert.AreEqual(0, writer.DevicePaths.Count);
    }

    [TestMethod]
    public void SynchronizeClampsEventPercentage()
    {
        var external = Display("EXT", @"\\.\DISPLAY2");
        var writer = new StubWriter();

        _ = new KeyboardBrightnessSyncService(writer).Synchronize(
            120,
            [DdcProbe(external, true)],
            [WmiProbe(external, WmiBrightnessProbeStatus.NotAvailable)]);

        Assert.AreEqual(100, writer.RequestedPercents.Single());
    }

    [TestMethod]
    public void AdjustByChangesInternalAndExternalDisplaysFromTheirCurrentValues()
    {
        var panel = Display("PANEL", @"\\.\DISPLAY1");
        var external = Display("EXT", @"\\.\DISPLAY2");
        var ddcWriter = new StubWriter();
        var wmiWriter = new StubWriter();

        var results = new KeyboardBrightnessSyncService(ddcWriter, wmiWriter).AdjustBy(
            -10,
            [DdcProbe(panel, true), DdcProbe(external, true)],
            [WmiProbe(panel, WmiBrightnessProbeStatus.ReadSucceeded),
                WmiProbe(external, WmiBrightnessProbeStatus.NotAvailable)]);

        Assert.AreEqual(2, results.Count);
        Assert.AreEqual(40, wmiWriter.RequestedPercents.Single());
        Assert.AreEqual(40, ddcWriter.RequestedPercents.Single());
    }

    [TestMethod]
    public void AdjustByClampsEachDisplayAtBrightnessLimits()
    {
        var panel = Display("PANEL", @"\\.\DISPLAY1");
        var writer = new StubWriter();

        _ = new KeyboardBrightnessSyncService(writer, writer).AdjustBy(
            -80,
            [DdcProbe(panel, true)],
            [WmiProbe(panel, WmiBrightnessProbeStatus.ReadSucceeded)]);

        Assert.AreEqual(0, writer.RequestedPercents.Single());
    }

    private static MonitorDisplayInfo Display(string identifier, string gdiName) =>
        new($@"\\?\DISPLAY#{identifier}#4&abc&0&UID111#{{guid}}", gdiName, identifier, 1);

    private static MonitorDdcProbeInfo DdcProbe(MonitorDisplayInfo display, bool readable) =>
        new(display, [new DdcBrightnessProbeResult(
            display.GdiDeviceName,
            display.FriendlyName,
            readable ? DdcBrightnessProbeStatus.ReadSucceeded : DdcBrightnessProbeStatus.ReadFailed,
            50,
            100,
            0)]);

    private static WmiBrightnessProbeResult WmiProbe(
        MonitorDisplayInfo display,
        WmiBrightnessProbeStatus status) =>
        new(display, status, display.DevicePath, 50, 101);

    private sealed class StubWriter : IBrightnessWriter
    {
        public List<string> DevicePaths { get; } = [];

        public List<int> RequestedPercents { get; } = [];

        public BrightnessWriteResult WriteBrightness(
            MonitorDisplayInfo display,
            int requestedPercent)
        {
            DevicePaths.Add(display.DevicePath);
            RequestedPercents.Add(requestedPercent);
            return new BrightnessWriteResult(
                BrightnessWriteProvider.DdcCi,
                BrightnessWriteStatus.WriteSucceeded,
                requestedPercent,
                requestedPercent,
                requestedPercent);
        }
    }
}
