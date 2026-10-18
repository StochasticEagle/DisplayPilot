using DisplayPilot.Display.Mccs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DisplayPilot.Display.Tests.Mccs;

[TestClass]
public sealed class MccsCapabilitiesParserTests
{
    [TestMethod]
    public void EmptyInputHasNoAdvertisedFeatures()
    {
        foreach (string? source in new string?[] { null, "", "   " })
        {
            var result = MccsCapabilitiesParser.Parse(source);
            Assert.AreEqual(0, result.Capabilities.Features.Count);
            Assert.IsFalse(result.IsValid);
            Assert.IsFalse(result.HasErrors);
        }
    }

    [TestMethod]
    public void ReadsStandardMonitorSections()
    {
        const string source =
            "(prot(monitor)type(lcd)model(LabPanel)mccs_ver(2.2)" +
            "cmds(01 03 0C)vcp(10 12 14(05 08) 60(0F 11)))";
        var profile = MccsCapabilitiesParser.Parse(source).Capabilities;

        Assert.AreEqual(source, profile.Raw);
        Assert.AreEqual("monitor", profile.Protocol);
        Assert.AreEqual("lcd", profile.Type);
        Assert.AreEqual("LabPanel", profile.Model);
        Assert.AreEqual("2.2", profile.MccsVersion);
        CollectionAssert.AreEqual(new byte[] { 0x01, 0x03, 0x0C }, profile.SupportedCommands);
        Assert.AreEqual(4, profile.Features.Count);
        Assert.IsTrue(profile.SupportsVcp(0x10));
        Assert.IsTrue(profile.SupportsVcp(0x12));
        Assert.IsFalse(profile.SupportsVcp(0x62));
    }

    [TestMethod]
    public void DistinguishesContinuousAndDiscreteVcpFeatures()
    {
        var profile = MccsCapabilitiesParser.Parse("(vcp(10 14(05 08) 60(0F 11)))").Capabilities;

        Assert.IsTrue(profile.FindFeature(0x10)!.Value.IsContinuous);
        Assert.IsTrue(profile.FindFeature(0x14)!.Value.HasDiscreteValues);
        CollectionAssert.AreEqual(new int[] { 5, 8 }, profile.FeatureValues(0x14)!.ToArray());
        CollectionAssert.AreEqual(new int[] { 15, 17 }, profile.FeatureValues(0x60)!.ToArray());
        Assert.IsNull(profile.FindFeature(0x20));
        Assert.IsNull(profile.FeatureValues(0x20));
    }

    [TestMethod]
    public void SupportsUnwrappedAndPackedHexSections()
    {
        var profile = MccsCapabilitiesParser.Parse("prot(monitor)cmds(01030C)vcp(101214)").Capabilities;

        Assert.AreEqual("monitor", profile.Protocol);
        Assert.AreEqual(3, profile.SupportedCommands.Count);
        Assert.AreEqual(3, profile.Features.Count);
        Assert.AreEqual("0x10, 0x12, 0x14", profile.FormatVcpCodes());
    }

    [TestMethod]
    public void NamedFeatureKeepsItsExistingDiscreteValues()
    {
        var profile = MccsCapabilitiesParser.Parse(
            "(vcp(60(0F 11))vcpname(60(HDMI Input)))").Capabilities;

        var feature = profile.FindFeature(0x60);
        Assert.IsNotNull(feature);
        Assert.AreEqual("HDMI Input", feature.Value.Name);
        CollectionAssert.AreEqual(new int[] { 15, 17 }, feature.Value.SupportedValues.ToArray());
    }

    [TestMethod]
    public void ReadsNestedWindowGeometry()
    {
        var profile = MccsCapabilitiesParser.Parse(
            "(window1(type(PIP)area(10 20 410 320)max(400 300)" +
            "min(100 80)window(7)))").Capabilities;

        Assert.IsTrue(profile.HasWindows);
        Assert.AreEqual(1, profile.Windows.Count);
        var window = profile.Windows[0];
        Assert.AreEqual(1, window.WindowNumber);
        Assert.AreEqual("PIP", window.Type);
        Assert.AreEqual(10, window.Area.X1);
        Assert.AreEqual(20, window.Area.Y1);
        Assert.AreEqual(400, window.Area.Width);
        Assert.AreEqual(300, window.Area.Height);
        Assert.AreEqual(400, window.MaxSize.Width);
        Assert.AreEqual(80, window.MinSize.Height);
        Assert.AreEqual(7, window.WindowId);
    }

    [TestMethod]
    public void UnknownSectionDoesNotChangeRecognizedValues()
    {
        var result = MccsCapabilitiesParser.Parse(
            "(unknown(test)value(42)vcp(10 12)other(ignored))");

        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(2, result.Capabilities.Features.Count);
        Assert.IsFalse(result.Capabilities.HasWindows);
    }

    [TestMethod]
    public void WindowWithoutOptionalFieldsHasDefaultGeometry()
    {
        var profile = MccsCapabilitiesParser.Parse("window2(type(PBP))").Capabilities;
        Assert.AreEqual(2, profile.Windows[0].WindowNumber);
        Assert.AreEqual("PBP", profile.Windows[0].Type);
        Assert.AreEqual(0, profile.Windows[0].Area.Width);
    }
}
