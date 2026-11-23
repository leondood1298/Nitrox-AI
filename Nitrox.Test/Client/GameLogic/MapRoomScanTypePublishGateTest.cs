using Nitrox.Model.DataStructures;

namespace NitroxClient.GameLogic;

[TestClass]
public sealed class MapRoomScanTypePublishGateTest
{
    [TestMethod]
    public void RepeatedResourceEventsWithTheSameTypesSendOneSnapshot()
    {
        // Streaming in an area registers hundreds of resources of types the room already lists.
        MapRoomScanTypePublishGate gate = new();
        NitroxId room = new();
        HashSet<TechType> types = [TechType.Quartz, TechType.Titanium];

        int sent = 0;
        for (int resource = 0; resource < 300; resource++)
        {
            if (gate.ShouldPublish(room, types, 300f, now: 10f + resource * 0.01f))
            {
                sent++;
            }
        }

        Assert.AreEqual(1, sent);
    }

    [TestMethod]
    public void NewTypeRemovedTypeAndRangeChangeEachSendImmediately()
    {
        MapRoomScanTypePublishGate gate = new();
        NitroxId room = new();
        HashSet<TechType> types = [TechType.Quartz];
        Assert.IsTrue(gate.ShouldPublish(room, types, 300f, 1f));

        types.Add(TechType.Titanium);
        Assert.IsTrue(gate.ShouldPublish(room, types, 300f, 1.1f), "A newly discovered type must be published.");
        Assert.IsFalse(gate.ShouldPublish(room, types, 300f, 1.2f));

        types.Remove(TechType.Quartz);
        Assert.IsTrue(gate.ShouldPublish(room, types, 300f, 1.3f), "A type with no resources left must be published.");

        Assert.IsTrue(gate.ShouldPublish(room, types, 350f, 1.4f), "A range upgrade changes which resources are in range.");
        Assert.IsFalse(gate.ShouldPublish(room, types, 350f, 1.5f));
    }

    [TestMethod]
    public void UnchangedTypesAreRefreshedAfterTheIntervalAndRoomsAreIndependent()
    {
        MapRoomScanTypePublishGate gate = new();
        NitroxId first = new();
        NitroxId second = new();
        HashSet<TechType> types = [TechType.Quartz];
        Assert.IsTrue(gate.ShouldPublish(first, types, 300f, 0f));

        Assert.IsTrue(gate.ShouldPublish(second, types, 300f, 0.1f), "Another room has its own state.");
        Assert.IsFalse(gate.ShouldPublish(first, types, 300f, MapRoomScanTypePublishGate.UNCHANGED_REFRESH_SECONDS - 0.1f));
        Assert.IsTrue(gate.ShouldPublish(first, types, 300f, MapRoomScanTypePublishGate.UNCHANGED_REFRESH_SECONDS + 0.1f));
    }
}
