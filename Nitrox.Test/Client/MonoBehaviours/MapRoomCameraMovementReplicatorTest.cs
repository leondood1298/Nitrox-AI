using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace Nitrox.Test.Client.MonoBehaviours;

[TestClass]
public sealed class MapRoomCameraMovementReplicatorTest
{
    private const float FRAME_SECONDS = 1f / 30f;

    [TestMethod]
    public void EngineOfACameraAnotherPlayerFliesIsHeardWhileItMovesFasterThanTheGamesThreshold()
    {
        Assert.IsTrue(MapRoomCameraMovementReplicator.EngineRuns(Vector3.zero, new Vector3(0f, 0f, 0.1f), FRAME_SECONDS), "3 metres per second.");
        Assert.IsFalse(MapRoomCameraMovementReplicator.EngineRuns(Vector3.zero, new Vector3(0f, 0.05f, 0f), FRAME_SECONDS),
            "At 1.5 metres per second the game leaves the engine of a camera silent as well.");
        Assert.IsFalse(MapRoomCameraMovementReplicator.EngineRuns(new Vector3(4f, 5f, 6f), new Vector3(4f, 5f, 6f), FRAME_SECONDS), "A camera that hovers only hums.");
    }

    [TestMethod]
    public void FrameThatTookNoTimeSaysNothingAboutTheSpeedOfACamera()
    {
        // The game's time stands still in a paused game.
        Assert.IsFalse(MapRoomCameraMovementReplicator.EngineRuns(Vector3.zero, new Vector3(0f, 0f, 1f), 0f));
    }
}
