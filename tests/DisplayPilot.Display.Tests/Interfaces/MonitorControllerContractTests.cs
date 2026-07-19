using DisplayPilot.Display.Interfaces;
using DisplayPilot.Display.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Monitor = DisplayPilot.Display.Models.Monitor;

namespace DisplayPilot.Display.Tests.Interfaces;

[TestClass]
public sealed class MonitorControllerContractTests
{
    [TestMethod]
    public async Task NonDdcControllerReturnsUnsupportedExtendedFeatures()
    {
        using IMonitorController controller = new BrightnessOnlyController();
        var monitor = new Monitor();

        var value = await controller.GetContrastAsync(monitor);
        var result = await controller.SetInputSourceAsync(monitor, 0x11);

        Assert.IsFalse(value.IsValid);
        Assert.IsFalse(result.IsSuccess);
        StringAssert.Contains(result.ErrorMessage, "not supported");
    }

    private sealed class BrightnessOnlyController : IMonitorController
    {
        public string Name => "Brightness only";

        public Task<VcpFeatureValue> GetBrightnessAsync(
            Monitor monitor,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new VcpFeatureValue(monitor.CurrentBrightness, 100));

        public Task<MonitorOperationResult> SetBrightnessAsync(
            Monitor monitor,
            int brightness,
            CancellationToken cancellationToken = default)
        {
            monitor.CurrentBrightness = brightness;
            return Task.FromResult(MonitorOperationResult.Success());
        }

        public void Dispose()
        {
        }
    }
}
