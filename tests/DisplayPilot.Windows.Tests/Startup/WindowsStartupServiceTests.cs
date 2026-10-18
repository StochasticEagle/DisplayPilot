// SPDX-License-Identifier: Unlicense
using DisplayPilot.Windows.Startup;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DisplayPilot.Windows.Tests.Startup;

[TestClass]
public sealed class WindowsStartupServiceTests
{
    [TestMethod]
    public void BuildStartupShortcutPathAddsShortcutName()
    {
        var result = WindowsStartupService.BuildStartupShortcutPath(
            @"C:\Users\dev\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup");

        Assert.AreEqual(
            "C:\\Users\\dev\\AppData\\Roaming\\Microsoft\\Windows\\Start Menu\\Programs\\Startup\\DisplayPilot.lnk",
            result);
    }

    [TestMethod]
    public void BuildStartupShortcutPathRejectsEmptyPath()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => WindowsStartupService.BuildStartupShortcutPath(string.Empty));
    }
}
