// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using DisplayPilot.Windows.Startup;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DisplayPilot.Windows.Tests.Startup;

[TestClass]
public sealed class WindowsStartupServiceTests
{
    [TestMethod]
    public void BuildCommonStartupShortcutPathAddsShortcutName()
    {
        var result = WindowsStartupService.BuildCommonStartupShortcutPath(
            @"C:\ProgramData");

        Assert.AreEqual(
            "C:\\ProgramData\\Microsoft\\Windows\\Start Menu\\Programs\\Startup\\DisplayPilot.lnk",
            result);
    }

    [TestMethod]
    public void BuildCommonStartupShortcutPathRejectsEmptyPath()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => WindowsStartupService.BuildCommonStartupShortcutPath(string.Empty));
    }
}
