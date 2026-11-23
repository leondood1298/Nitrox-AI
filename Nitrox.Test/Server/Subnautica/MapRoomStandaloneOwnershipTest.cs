using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Packet = Nitrox.Model.Packets.Packet;

namespace Nitrox.Test.Server.Subnautica;

/// <summary>
///     A Scanner Room can be the first piece of a new base. It then reaches the server nested inside the base instead
///     of as a built piece of an existing one, and still needs a simulation owner: scan progress, scan results and the
///     docking of its two default cameras are only accepted from that owner.
/// </summary>
[TestClass]
public sealed class MapRoomStandaloneOwnershipTest
{
    private static readonly NitroxId baseId = new("00000000-0000-0000-0000-000000000300");
    private static readonly NitroxId standaloneRoomId = baseId.Increment();

    [TestMethod]
    public async Task RoomThatStartsABaseIsSimulatedByItsBuilder()
    {
        ScannerRoomScenarioFixture scenario = new();

        IReadOnlyList<Packet> sent = await PlaceStandaloneRoomAsync(scenario);

        Assert.IsTrue(scenario.Ownership.TryGetLock(standaloneRoomId, out SimulationOwnershipData.PlayerLock roomLock),
            "Without an owner nobody may publish this room's scan state or dock its default cameras.");
        Assert.AreSame(scenario.PlayerA, roomLock.Player);
        Assert.IsTrue(sent.OfType<SimulationOwnershipChange>().Any(change =>
                change.Entities.Any(entity => entity.Id == standaloneRoomId && entity.SessionId == scenario.PlayerA.SessionId)),
            "Every player has to learn who simulates the room.");
    }

    [TestMethod]
    public async Task JoiningPlayerIsToldWhoSimulatesARoomThatStartedABase()
    {
        ScannerRoomScenarioFixture scenario = new();
        await PlaceStandaloneRoomAsync(scenario);

        List<SimulatedEntity> joinData = scenario.EntitySimulation.AssignGlobalRootEntitiesAndGetData(scenario.PlayerB);

        SimulatedEntity room = joinData.Single(entity => entity.Id == standaloneRoomId);
        Assert.AreEqual(scenario.PlayerA.SessionId, room.SessionId);
    }

    [TestMethod]
    public async Task JoiningPlayerTakesOverARoomNobodySimulates()
    {
        ScannerRoomScenarioFixture scenario = new();
        await PlaceStandaloneRoomAsync(scenario);
        // The builder left while alone on the server.
        Assert.IsTrue(scenario.Ownership.RevokeOwnerOfId(standaloneRoomId));

        List<SimulatedEntity> joinData = scenario.EntitySimulation.AssignGlobalRootEntitiesAndGetData(scenario.PlayerB);

        Assert.AreEqual(scenario.PlayerB.SessionId, joinData.Single(entity => entity.Id == standaloneRoomId).SessionId);
        Assert.AreSame(scenario.PlayerB, scenario.Ownership.GetPlayerForLock(standaloneRoomId));
    }

    [TestMethod]
    public async Task DeconstructedRoomIsNoLongerHandedToJoiningPlayers()
    {
        ScannerRoomScenarioFixture scenario = new();
        await PlaceStandaloneRoomAsync(scenario);
        Assert.IsTrue(scenario.EntityRegistry.TryGetEntityById(standaloneRoomId, out MapRoomEntity room));
        scenario.WorldEntityManager.RemoveGlobalRootEntity(baseId);

        await scenario.CleanupAsync(room);

        Assert.IsFalse(scenario.EntitySimulation.AssignGlobalRootEntitiesAndGetData(scenario.PlayerB).Any(entity => entity.Id == standaloneRoomId),
            "A lock on a room that no longer exists would never be released.");
    }

    private static async Task<IReadOnlyList<Packet>> PlaceStandaloneRoomAsync(ScannerRoomScenarioFixture scenario)
    {
        GhostEntity ghost = GhostEntity.MakeEmpty();
        ghost.Id = baseId;
        ghost.TechType = new NitroxTechType("BaseMapRoom");
        scenario.WorldEntityManager.AddOrUpdateGlobalRootEntity(ghost);

        // The client gives the base the ghost's id and the room the next one (BuildUtils.TryTransferIdFromGhostToModule).
        BuildEntity buildEntity = BuildEntity.MakeEmpty();
        buildEntity.Id = baseId;
        buildEntity.ChildEntities.Add(new MapRoomEntity(standaloneRoomId, baseId, new NitroxInt3(0, 0, 0)));

        return await scenario.PlaceBaseAsync(scenario.PlayerA, new PlaceBase(baseId, buildEntity));
    }
}
