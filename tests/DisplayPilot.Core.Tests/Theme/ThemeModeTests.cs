using DisplayPilot.Core.Theme;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DisplayPilot.Core.Tests.Theme;

[TestClass]
public sealed class ThemeModeTests
{
    [TestMethod]
    public void ThemeModesHaveStableSerializedNames()
    {
        Assert.AreEqual("Light", ThemeMode.Light.ToString());
        Assert.AreEqual("Dark", ThemeMode.Dark.ToString());
    }
}
