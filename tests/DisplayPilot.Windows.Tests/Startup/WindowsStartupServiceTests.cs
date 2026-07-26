// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using DisplayPilot.Windows.Startup;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DisplayPilot.Windows.Tests.Startup;

[TestClass]
public sealed class WindowsStartupServiceTests
{
    [TestMethod]
    public void BuildCommandLineQuotesExecutablePathAndAddsStartupArgument()
    {
        var result = WindowsStartupService.BuildCommandLine(
            @"C:\Program Files\DisplayPilot\DisplayPilot.exe");

        Assert.AreEqual(
            "\"C:\\Program Files\\DisplayPilot\\DisplayPilot.exe\" --startup",
            result);
    }

    [TestMethod]
    public void BuildCommandLineRejectsEmptyPath()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => WindowsStartupService.BuildCommandLine(string.Empty));
    }
}
