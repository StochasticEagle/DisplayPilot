using DisplayPilot.Display.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DisplayPilot.Display.Tests.Models;

[TestClass]
public sealed class MonitorOperationResultTests
{
    [TestMethod]
    public void SuccessContainsNoError()
    {
        var result = MonitorOperationResult.Success();

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNull(result.ErrorMessage);
        Assert.IsNull(result.ErrorCode);
    }

    [TestMethod]
    public void FailurePreservesErrorDetails()
    {
        var result = MonitorOperationResult.Failure("DDC/CI command failed", errorCode: 31);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual("DDC/CI command failed", result.ErrorMessage);
        Assert.AreEqual(31, result.ErrorCode);
    }
}
