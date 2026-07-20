// Copyright (c) 2026 Aaron
// Licensed under the MIT license. See the LICENSE file in the project root.

using DisplayPilot.Windows.Scheduling;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DisplayPilot.Windows.Tests.Scheduling;

[TestClass]
public sealed class WindowsBoundaryTimerTests
{
    [TestMethod]
    public void ArmAndCancelTrackTimerState()
    {
        using var timer = new WindowsBoundaryTimer();
        var dueTime = DateTimeOffset.Now.AddMinutes(5);

        timer.Arm(dueTime);

        Assert.IsTrue(timer.IsArmed);
        Assert.AreEqual(dueTime, timer.DueTime);

        timer.Cancel();

        Assert.IsFalse(timer.IsArmed);
        Assert.IsNull(timer.DueTime);
    }

    [TestMethod]
    public void OneShotTimerRaisesElapsedOnceAndDisarms()
    {
        using var timer = new WindowsBoundaryTimer();
        using var elapsed = new ManualResetEventSlim();
        var elapsedCount = 0;
        timer.Elapsed += (_, _) =>
        {
            _ = Interlocked.Increment(ref elapsedCount);
            elapsed.Set();
        };

        timer.Arm(DateTimeOffset.Now.AddMilliseconds(100));

        Assert.IsTrue(elapsed.Wait(TimeSpan.FromSeconds(5)), "The Windows timer did not expire.");
        Assert.AreEqual(1, Volatile.Read(ref elapsedCount));
        Assert.IsFalse(timer.IsArmed);
        Assert.IsNull(timer.DueTime);
    }
}
