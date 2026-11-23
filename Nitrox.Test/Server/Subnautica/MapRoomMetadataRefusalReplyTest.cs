using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Packet = Nitrox.Model.Packets.Packet;

namespace Nitrox.Test.Server.Subnautica;

/// <summary>
///     A client applies its own scan target or progress before the server has seen it. When the server refuses the
///     request, the client has to be told what the room's state really is: nobody else's update will, and with a stale
///     generation or revision all its later requests would be refused as well.
/// </summary>
[TestClass]
public sealed class MapRoomMetadataRefusalReplyTest
{
    private static readonly NitroxTechType limestone = new("LimestoneChunk");
    private static readonly NitroxTechType shale = new("ShaleChunk");

    [TestMethod]
    public async Task RequestWithAStaleRevisionIsAnsweredWithTheRoomsRealState()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.AreEqual(0, (await scenario.UpdateMetadataAsync(scenario.PlayerA, ScannerRoomScenarioFixture.RoomId, new MapRoomMetadata(limestone, 0))).Count,
            "An accepted request is relayed, not answered.");

        // Player B had not received that change yet when it picked another target.
        IReadOnlyList<Packet> replies = await scenario.UpdateMetadataAsync(scenario.PlayerB, ScannerRoomScenarioFixture.RoomId, new MapRoomMetadata(shale, 0));

        EntityMetadataUpdate reply = replies.OfType<EntityMetadataUpdate>().Single();
        Assert.AreEqual(ScannerRoomScenarioFixture.RoomId, reply.Id);
        MapRoomMetadata canonical = (MapRoomMetadata)reply.NewValue;
        Assert.AreEqual(limestone, canonical.TypeToScan);
        Assert.AreEqual(1, canonical.Generation);
        Assert.AreEqual(1, canonical.Revision);
        Assert.AreEqual(limestone, ((MapRoomMetadata)scenario.Room.Metadata).TypeToScan, "The refused request changed nothing.");
    }

    [TestMethod]
    public async Task RefusedRequestCanBeRepeatedWithTheStateFromTheAnswer()
    {
        ScannerRoomScenarioFixture scenario = new();
        await scenario.UpdateMetadataAsync(scenario.PlayerA, ScannerRoomScenarioFixture.RoomId, new MapRoomMetadata(limestone, 0));
        IReadOnlyList<Packet> replies = await scenario.UpdateMetadataAsync(scenario.PlayerB, ScannerRoomScenarioFixture.RoomId, new MapRoomMetadata(shale, 0));
        MapRoomMetadata canonical = (MapRoomMetadata)replies.OfType<EntityMetadataUpdate>().Single().NewValue;

        await scenario.UpdateMetadataAsync(scenario.PlayerB, ScannerRoomScenarioFixture.RoomId,
            new MapRoomMetadata(shale, 0, canonical.Generation, canonical.Revision));

        Assert.AreEqual(shale, ((MapRoomMetadata)scenario.Room.Metadata).TypeToScan);
    }

    [TestMethod]
    public async Task ProgressFromAPlayerWhoDoesNotSimulateTheRoomIsAnsweredWithTheRoomsRealState()
    {
        ScannerRoomScenarioFixture scenario = new();
        Assert.IsTrue(scenario.Ownership.TryAcquire(ScannerRoomScenarioFixture.RoomId, scenario.PlayerA, SimulationLockType.TRANSIENT));
        await scenario.UpdateMetadataAsync(scenario.PlayerA, ScannerRoomScenarioFixture.RoomId, new MapRoomMetadata(limestone, 0));

        IReadOnlyList<Packet> replies = await scenario.UpdateMetadataAsync(scenario.PlayerB, ScannerRoomScenarioFixture.RoomId,
            new MapRoomMetadata(limestone, 7, 1, 1));

        MapRoomMetadata canonical = (MapRoomMetadata)replies.OfType<EntityMetadataUpdate>().Single().NewValue;
        Assert.AreEqual(0, canonical.NumNodesScanned);
        Assert.AreEqual(0, ((MapRoomMetadata)scenario.Room.Metadata).NumNodesScanned);
    }

    [TestMethod]
    public async Task RoomThatNeverScannedAnswersWithAnIdleState()
    {
        ScannerRoomScenarioFixture scenario = new();

        IReadOnlyList<Packet> replies = await scenario.UpdateMetadataAsync(scenario.PlayerB, ScannerRoomScenarioFixture.RoomId,
            new MapRoomMetadata(shale, 0, 5, 9));

        MapRoomMetadata canonical = (MapRoomMetadata)replies.OfType<EntityMetadataUpdate>().Single().NewValue;
        Assert.AreEqual(NitroxTechType.None, canonical.TypeToScan);
        Assert.AreEqual(0, canonical.Generation);
        Assert.AreEqual(0, canonical.Revision);
    }
}
