using DisplayPilot.Display.Rotation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DisplayPilot.Display.Tests.Rotation;

[TestClass]
public sealed class WindowsDisplayRotationServiceTests
{
    [DataTestMethod]
    [DataRow(DisplayRotation.Landscape, DisplayRotation.Portrait, true)]
    [DataRow(DisplayRotation.Landscape, DisplayRotation.PortraitFlipped, true)]
    [DataRow(DisplayRotation.Portrait, DisplayRotation.LandscapeFlipped, true)]
    [DataRow(DisplayRotation.Portrait, DisplayRotation.PortraitFlipped, false)]
    [DataRow(DisplayRotation.Landscape, DisplayRotation.LandscapeFlipped, false)]
    public void DimensionSwapTracksLandscapePortraitBoundary(
        DisplayRotation current,
        DisplayRotation requested,
        bool expected)
    {
        Assert.AreEqual(
            expected,
            WindowsDisplayRotationService.RequiresDimensionSwap(current, requested));
    }
}
