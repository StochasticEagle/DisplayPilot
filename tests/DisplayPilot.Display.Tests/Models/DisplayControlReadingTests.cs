using DisplayPilot.Display.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DisplayPilot.Display.Tests.Models;

[TestClass]
public sealed class DisplayControlReadingTests
{
    [TestMethod]
    public void ConvertsNonstandardRawRangeToPercentage()
    {
        var value = new DisplayControlReading(current: 25, minimum: 0, maximum: 50);

        Assert.IsTrue(value.IsValid);
        Assert.AreEqual(50, value.ToPercentage());
    }

    [TestMethod]
    public void ConvertsPercentageToNonstandardRawRange()
    {
        Assert.AreEqual(25, DisplayControlReading.FromPercentage(50, maximum: 50));
    }

    [DataTestMethod]
    [DataRow(-10, 0)]
    [DataRow(110, 50)]
    public void ClampsPercentageBeforeConverting(int percentage, int expected)
    {
        Assert.AreEqual(expected, DisplayControlReading.FromPercentage(percentage, maximum: 50));
    }

    [TestMethod]
    public void InvalidRangeProducesInvalidValue()
    {
        var value = new DisplayControlReading(current: 10, minimum: 10, maximum: 10);

        Assert.IsFalse(value.IsValid);
        Assert.AreEqual(0, value.ToPercentage());
    }
}
