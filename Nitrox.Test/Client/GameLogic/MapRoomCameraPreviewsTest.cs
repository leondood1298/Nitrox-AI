using NitroxClient.GameLogic;
using UnityEngine;

namespace Nitrox.Test.Client.GameLogic;

/// <summary>
///     When the picture the game takes of a camera's view is sent, with a routine that needs no game.
/// </summary>
[TestClass]
public sealed class MapRoomCameraPreviewsTest
{
    [TestMethod]
    public void PublishesAfterEndOfFrameYieldAndBeforeVanillaExitContinuation()
    {
        List<string> events = [];
        IEnumerator wrapped = MapRoomCameraPreviews.PublishAfterCapturedFrame(
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
        IEnumerator wrapped = MapRoomCameraPreviews.PublishAfterCapturedFrame(
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
