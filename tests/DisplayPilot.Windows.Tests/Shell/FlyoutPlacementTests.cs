// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using DisplayPilot.Windows.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DisplayPilot.Windows.Tests.Shell;

[TestClass]
public sealed class FlyoutPlacementTests
{
    private static readonly NotificationAreaBounds WorkArea = new(0, 0, 1920, 1040);

    [TestMethod]
    public void BottomTaskbarPlacesFlyoutAboveIcon()
    {
        var result = FlyoutPlacement.Calculate(
            new NotificationAreaBounds(1800, 1040, 1840, 1080),
            WorkArea,
            480,
            520);

        Assert.AreEqual(new NotificationAreaBounds(1360, 520, 1840, 1040), result);
    }

    [TestMethod]
    public void TopTaskbarPlacesFlyoutBelowTaskbar()
    {
        var result = FlyoutPlacement.Calculate(
            new NotificationAreaBounds(1800, -40, 1840, 0),
            WorkArea,
            480,
            520);

        Assert.AreEqual(new NotificationAreaBounds(1360, 0, 1840, 520), result);
    }

    [TestMethod]
    public void RightTaskbarPlacesFlyoutInsideRightEdge()
    {
        var result = FlyoutPlacement.Calculate(
            new NotificationAreaBounds(1920, 900, 1960, 940),
            WorkArea,
            480,
            520);

        Assert.AreEqual(new NotificationAreaBounds(1440, 420, 1920, 940), result);
    }

    [TestMethod]
    public void LeftTaskbarPlacesFlyoutInsideLeftEdge()
    {
        var result = FlyoutPlacement.Calculate(
            new NotificationAreaBounds(-40, 900, 0, 940),
            WorkArea,
            480,
            520);

        Assert.AreEqual(new NotificationAreaBounds(0, 420, 480, 940), result);
    }
}
