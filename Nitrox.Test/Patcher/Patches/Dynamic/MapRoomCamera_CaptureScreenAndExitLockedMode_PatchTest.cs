using System.Collections;
using NitroxPatcher.Patches.Dynamic;
using UnityEngine;

namespace Nitrox.Test.Patcher.Patches.Dynamic;

[TestClass]
public sealed class MapRoomCamera_CaptureScreenAndExitLockedMode_PatchTest
{
    [TestMethod]
    public void PublishesAfterEndOfFrameYieldAndBeforeVanillaExitContinuation()
    {
        List<string> events = [];
        IEnumerator wrapped = MapRoomCamera_CaptureScreenAndExitLockedMode_Patch.PublishAfterCapturedFrame(
            VanillaCapture(events), () => events.Add("publish"));

        Assert.IsTrue(wrapped.MoveNext());
        Assert.IsInstanceOfType<WaitForEndOfFrame>(wrapped.Current);
        CollectionAssert.AreEqual(new[] { "capture_queued" }, events);

        Assert.IsFalse(wrapped.MoveNext());
        CollectionAssert.AreEqual(new[] { "capture_queued", "publish", "exit" }, events);
    }

    [TestMethod]
    public void PublishesAtMostOnceAndAlwaysFinishesWrappedRoutine()
    {
        int publications = 0;
        IEnumerator wrapped = MapRoomCamera_CaptureScreenAndExitLockedMode_Patch.PublishAfterCapturedFrame(
            TwoFrames(), () => publications++);

        while (wrapped.MoveNext())
        {
        }

        Assert.AreEqual(1, publications);
    }

    private static IEnumerator VanillaCapture(List<string> events)
    {
        events.Add("capture_queued");
        yield return new WaitForEndOfFrame();
        events.Add("exit");
    }

    private static IEnumerator TwoFrames()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
    }
}
