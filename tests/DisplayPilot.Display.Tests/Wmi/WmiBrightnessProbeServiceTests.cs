// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using DisplayPilot.Display.Discovery;
using DisplayPilot.Display.Wmi;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DisplayPilot.Display.Tests.Wmi;

[TestClass]
public sealed class WmiBrightnessProbeServiceTests
{
    [TestMethod]
    public void ProbeBrightness_CorrelatesActivePanelByStableIdentity()
    {
        var display = Display(@"\\?\DISPLAY#BOE0900#4&abc&0&UID111#{guid}");
        var api = new StubApi(new WmiBrightnessQueryResult([
            new(@"DISPLAY\BOE0900\4&abc&0&UID111_0", true, 42, [0, 25, 50, 75, 100]),
        ]));

        var result = new WmiBrightnessProbeService(api).ProbeBrightness([display]).Single();

        Assert.AreEqual(WmiBrightnessProbeStatus.ReadSucceeded, result.Status);
        Assert.AreEqual((byte)42, result.CurrentBrightness);
        Assert.AreEqual(5, result.LevelCount);
    }

    [TestMethod]
    public void ProbeBrightness_DoesNotMatchSameModelWithDifferentUid()
    {
        var display = Display(@"\\?\DISPLAY#BOE0900#4&abc&0&UID222#{guid}");
        var api = new StubApi(new WmiBrightnessQueryResult([
            new(@"DISPLAY\BOE0900\4&abc&0&UID111_0", true, 42, [0, 100]),
        ]));

        var result = new WmiBrightnessProbeService(api).ProbeBrightness([display]).Single();

        Assert.AreEqual(WmiBrightnessProbeStatus.NotAvailable, result.Status);
    }

    [TestMethod]
    public void ProbeBrightness_ReportsInactiveMatchingInstance()
    {
        var display = Display(@"\\?\DISPLAY#BOE0900#4&abc&0&UID111#{guid}");
        var api = new StubApi(new WmiBrightnessQueryResult([
            new(@"DISPLAY\BOE0900\4&abc&0&UID111_0", false, 30, [0, 100]),
        ]));

        var result = new WmiBrightnessProbeService(api).ProbeBrightness([display]).Single();

        Assert.AreEqual(WmiBrightnessProbeStatus.Inactive, result.Status);
    }

    [TestMethod]
    public void ProbeBrightness_PropagatesQueryFailureWithoutThrowing()
    {
        var api = new StubApi(WmiBrightnessQueryResult.Failed(unchecked((int)0x8004100E), "Invalid namespace"));

        var result = new WmiBrightnessProbeService(api).ProbeBrightness([
            Display(@"\\?\DISPLAY#BOE0900#4&abc&0&UID111#{guid}"),
        ]).Single();

        Assert.AreEqual(WmiBrightnessProbeStatus.QueryFailed, result.Status);
        Assert.AreEqual(unchecked((int)0x8004100E), result.ErrorCode);
    }

    private static MonitorDisplayInfo Display(string path) =>
        new(path, @"\\.\DISPLAY1", "Internal Display", 1);

    private sealed class StubApi(WmiBrightnessQueryResult result) : IWmiBrightnessApi
    {
        public WmiBrightnessQueryResult QueryBrightness() => result;
    }
}
