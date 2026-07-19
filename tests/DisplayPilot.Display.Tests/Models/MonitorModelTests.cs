using DisplayPilot.Display.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DisplayPilot.Display.Tests.Models;

[TestClass]
public sealed class MonitorModelTests
{
    [DataTestMethod]
    [DataRow(-1, 0)]
    [DataRow(42, 42)]
    [DataRow(101, 100)]
    public void BrightnessIsClampedToPercentageRange(int requested, int expected)
    {
        var monitor = new Monitor { CurrentBrightness = requested };

        Assert.AreEqual(expected, monitor.CurrentBrightness);
    }

    [TestMethod]
    public void CapabilitiesDriveFeatureSupport()
    {
        var monitor = new Monitor
        {
            Capabilities = MonitorCapabilities.Brightness | MonitorCapabilities.Volume,
        };

        Assert.IsTrue(monitor.SupportsBrightness);
        Assert.IsFalse(monitor.SupportsContrast);
        Assert.IsTrue(monitor.SupportsVolume);
    }

    [TestMethod]
    public void ParsedVcpCapabilitiesDriveDiscreteFeatureSupport()
    {
        var monitor = new Monitor
        {
            VcpCapabilitiesInfo = new VcpCapabilities
            {
                SupportedVcpCodes = new Dictionary<byte, VcpCodeInfo>
                {
                    [0x60] = new(0x60, "Input Source", new[] { 0x0F, 0x11 }),
                    [0xD6] = new(0xD6, "Power Mode", new[] { 0x01, 0x04 }),
                },
            },
        };

        Assert.IsTrue(monitor.SupportsInputSource);
        Assert.IsTrue(monitor.SupportsPowerState);
    }
}
