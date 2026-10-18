// SPDX-License-Identifier: Unlicense
using System.Reflection;
using System.Runtime.InteropServices;
using DisplayPilot.Display.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DisplayPilot.Display.Tests.Interop;

[TestClass]
public sealed class NativeLayoutTests
{
    [DataTestMethod]
    [DataRow("DisplayAdapterId", 8)]
    [DataRow("ActivePathRecord", 72)]
    [DataRow("ModeRecord", 64)]
    [DataRow("DisplayNameHeader", 20)]
    [DataRow("SourceDisplayName", 84)]
    [DataRow("TargetDisplayName", 420)]
    [DataRow("DisplayMonitorInfo", 104)]
    public void Win32LayoutMatchesDocumentedSize(string name, int bytes)
    {
        var type = FindNativeType(name);
        Assert.AreEqual(bytes, Marshal.SizeOf(type), name);
    }

    [TestMethod]
    public void DisplayPathContainsCorrectSourceAndTargetLocations()
    {
        var type = FindNativeType("ActivePathRecord");
        Assert.AreEqual(0, Marshal.OffsetOf(type, "SourceAdapter").ToInt32());
        Assert.AreEqual(8, Marshal.OffsetOf(type, "SourceId").ToInt32());
        Assert.AreEqual(20, Marshal.OffsetOf(type, "TargetAdapter").ToInt32());
        Assert.AreEqual(28, Marshal.OffsetOf(type, "TargetId").ToInt32());
    }

    [TestMethod]
    public void PhysicalMonitorHandleIncludesNativeDescriptionBuffer()
    {
        var type = FindNativeType("DdcMonitorHandle");
        Assert.AreEqual(IntPtr.Size + 256, Marshal.SizeOf(type));
    }

    private static Type FindNativeType(string name) =>
        typeof(WindowsDisplayConfigApi).Assembly.GetType(
            $"DisplayPilot.Display.Interop.{name}", throwOnError: true)!;
}
