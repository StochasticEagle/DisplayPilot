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
        Assert.AreEqual("1,3,12", string.Join(",", profile.SupportedCommands));
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
        Assert.AreEqual("5,8", string.Join(",", profile.FeatureValues(0x14)!));
        Assert.AreEqual("15,17", string.Join(",", profile.FeatureValues(0x60)!));
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
        Assert.AreEqual("15,17", string.Join(",", feature.Value.SupportedValues));
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

    [DataTestMethod]
    [DataRow("vcp(10)", 1)]
    [DataRow("(vcp(10 12))", 2)]
    [DataRow("vcp(10 12 14 60)", 4)]
    [DataRow("vcp(10121460)", 4)]
    [DataRow("vcp( 10  12  14  60 )", 4)]
    [DataRow("VCP(10 12)", 2)]
    [DataRow("vCp(10 12)", 2)]
    [DataRow("vcp()", 0)]
    [DataRow("model(TestPanel)", 0)]
    [DataRow("unrelated(foo)", 0)]
    public void CapabilitySectionVariantsHaveExpectedFeatureCount(string text, int expected)
    {
        var profile = MccsCapabilitiesParser.Parse(text).Capabilities;
        Assert.AreEqual(expected, profile.Features.Count);
    }

    [DataTestMethod]
    [DataRow("vcp(10)", true)]
    [DataRow("vcp()", false)]
    [DataRow("prot(monitor)", false)]
    [DataRow("window1(type(PIP))", false)]
    [DataRow("", false)]
    public void ValidityRequiresAtLeastOneParsedVcpFeature(string text, bool expected)
    {
        var result = MccsCapabilitiesParser.Parse(text);
        Assert.AreEqual(expected, result.IsValid);
        Assert.IsFalse(result.HasErrors);
    }

    [TestMethod]
    public void DuplicateFeatureUsesTheMostRecentValues()
    {
        var profile = MccsCapabilitiesParser.Parse("vcp(60(0F) 60(11 12))").Capabilities;
        Assert.AreEqual(1, profile.Features.Count);
        Assert.AreEqual("17,18", string.Join(",", profile.FeatureValues(0x60)!));
    }

    [TestMethod]
    public void HexParsingAcceptsLowercaseDigitsAndSeparators()
    {
        var profile = MccsCapabilitiesParser.Parse(
            "cmds(0a,1b 2c)vcp(6a(0f 1a) af)").Capabilities;
        Assert.AreEqual("10,27,44", string.Join(",", profile.SupportedCommands));
        Assert.AreEqual("15,26", string.Join(",", profile.FeatureValues(0x6A)!));
        Assert.IsTrue(profile.SupportsVcp(0xAF));
    }

    [TestMethod]
    public void EmptyFeatureValuesRemainContinuous()
    {
        var profile = MccsCapabilitiesParser.Parse("vcp(10() 12)").Capabilities;
        Assert.IsTrue(profile.FindFeature(0x10)!.Value.IsContinuous);
        Assert.IsTrue(profile.FindFeature(0x12)!.Value.IsContinuous);
    }

    [TestMethod]
    public void SupportedCodeFormattingUsesNumericOrder()
    {
        var profile = MccsCapabilitiesParser.Parse("vcp(60 14 10 12)").Capabilities;
        Assert.AreEqual("0x10, 0x12, 0x14, 0x60", profile.FormatVcpCodes());
    }

    [TestMethod]
    public void UnknownNamedSectionIsIgnoredWithoutRemovingAdjacentFeatures()
    {
        var profile = MccsCapabilitiesParser.Parse(
            "vendor(extra(test))vcp(10 12)private(data)").Capabilities;
        Assert.AreEqual(2, profile.Features.Count);
        Assert.IsTrue(profile.SupportsVcp(0x10));
    }

    [TestMethod]
    public void UnclosedAndUnexpectedParenthesesNeverRaiseAnException()
    {
        foreach (var input in new[]
        {
            "vcp(10 12",
            "vcp(10(01 02)",
            ")))vcp(10)",
            "(model(X)vcp(10",
            "vcp(()",
            "window1(area(1 2",
        })
        {
            var result = MccsCapabilitiesParser.Parse(input);
            Assert.IsNotNull(result.Capabilities);
        }
    }

    [DataTestMethod]
    [DataRow("window1(type(PIP))", 1)]
    [DataRow("window9(type(PBP))", 9)]
    [DataRow("window23(type(PIP))", 23)]
    public void ParsesDifferentWindowNumbers(string source, int number)
    {
        var profile = MccsCapabilitiesParser.Parse(source).Capabilities;
        Assert.AreEqual(1, profile.Windows.Count);
        Assert.AreEqual(number, profile.Windows[0].WindowNumber);
    }

    [TestMethod]
    public void MultipleWindowSectionsKeepIndependentGeometry()
    {
        var profile = MccsCapabilitiesParser.Parse(
            "window1(type(PIP)area(0 0 200 100))" +
            "window2(type(PBP)area(10 20 400 300))").Capabilities;
        Assert.AreEqual(2, profile.Windows.Count);
        Assert.AreEqual(200, profile.Windows[0].Area.Width);
        Assert.AreEqual(390, profile.Windows[1].Area.Width);
        Assert.AreEqual("PBP", profile.Windows[1].Type);
    }

    [TestMethod]
    public void OptionalWindowFieldsAreParsedIndependently()
    {
        var profile = MccsCapabilitiesParser.Parse(
            "window4(min(20 30)max(600 400)window(15))").Capabilities;
        var window = profile.Windows[0];
        Assert.AreEqual(20, window.MinSize.Width);
        Assert.AreEqual(30, window.MinSize.Height);
        Assert.AreEqual(600, window.MaxSize.Width);
        Assert.AreEqual(400, window.MaxSize.Height);
        Assert.AreEqual(15, window.WindowId);
    }

    [TestMethod]
    public void WindowAreaHandlesNegativeCoordinates()
    {
        var window = MccsCapabilitiesParser.Parse(
            "window3(area(-20 -10 180 90))").Capabilities.Windows[0];
        Assert.AreEqual(200, window.Area.Width);
        Assert.AreEqual(100, window.Area.Height);
    }

    [TestMethod]
    public void InvalidWindowSuffixDoesNotAddAWindow()
    {
        var profile = MccsCapabilitiesParser.Parse("windowZ(type(PIP))vcp(10)").Capabilities;
        Assert.IsFalse(profile.HasWindows);
        Assert.IsTrue(profile.SupportsVcp(0x10));
    }

    [TestMethod]
    public void ParserDoesNotDiscardUnknownModelText()
    {
        const string source = "model(Lab Display 27\u2033 / Rev. B)vcp(10)";
        var profile = MccsCapabilitiesParser.Parse(source).Capabilities;
        Assert.AreEqual("Lab Display 27\u2033 / Rev. B", profile.Model);
        Assert.AreEqual(source, profile.Raw);
    }

    [TestMethod]
    public void LongerCapabilitiesSequencePreservesEveryAdvertisedCode()
    {
        var text = "prot(monitor)type(lcd)mccs_ver(2.2)" +
            "cmds(01 02 03 04 05 06 07 08 09 0A)" +
            "vcp(10 12 14(05 08) 16 18 1A 60(0F 11) 62 6C 6E 70 AC AE B6 D6 DF)";
        var profile = MccsCapabilitiesParser.Parse(text).Capabilities;
        Assert.AreEqual(16, profile.Features.Count);
        Assert.IsTrue(profile.SupportsVcp(0xDF));
        Assert.AreEqual("15,17", string.Join(",", profile.FeatureValues(0x60)!));
        Assert.AreEqual(10, profile.SupportedCommands.Count);
    }

    [TestMethod]
    public void VeryLongUnknownSectionIsSkippedWithoutRecursion()
    {
        var profile = MccsCapabilitiesParser.Parse(
            $"unknown({new string('x', 8192)})vcp(10)").Capabilities;
        Assert.IsTrue(profile.SupportsVcp(0x10));
    }

}
