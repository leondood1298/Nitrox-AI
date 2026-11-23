using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Test.Server.GameLogic;
using Nitrox.Test.Server.Helper;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

[TestClass]
public sealed class EntitySpawnedByClientProcessorTest
{
    private ScannerRoomScenarioFixture fixture;
    private WorldEntity crashFish;

    [TestInitialize]
    public void Init()
    {
        fixture = new ScannerRoomScenarioFixture(connectedPlayers: new ConnectedPlayers());
        crashFish = new WorldEntity(new NitroxVector3(10f, -20f, 30f), NitroxQuaternion.Identity, NitroxVector3.One, new NitroxTechType("Crash"), 0, "crash", false,
                                    new NitroxId(), null);
        foreach (Player player in new[] { fixture.PlayerA, fixture.PlayerB })
        {
            player.AddCells([crashFish.AbsoluteEntityCell]);
        }
    }

    [TestMethod]
    public async Task TheSpawningPlayerIsToldItSimulatesWhatItMade()
    {
        await fixture.SpawnByClientAsync(fixture.PlayerA, crashFish);

        List<SimulationOwnershipChange> toSpawner = fixture.SentTo<SimulationOwnershipChange>(fixture.PlayerA);
        Assert.AreEqual(1, toSpawner.Count);
        SimulatedEntity simulated = toSpawner[0].Entities.Single();
        Assert.AreEqual(crashFish.Id, simulated.Id);
        Assert.AreEqual(fixture.PlayerA.SessionId, simulated.SessionId);
        Assert.IsTrue(simulated.ChangesPosition);
        Assert.AreEqual(fixture.PlayerA, fixture.Ownership.GetPlayerForLock(crashFish.Id));
    }

    [TestMethod]
    public async Task TheOthersAreStillToldWhoSimulatesIt()
    {
        await fixture.SpawnByClientAsync(fixture.PlayerA, crashFish);

        Assert.AreEqual(1, fixture.SentToOthers<SimulationOwnershipChange>().Count);
        Assert.AreEqual(1, fixture.SentTo<SpawnEntities>(fixture.PlayerB).Count);
        Assert.AreEqual(0, fixture.SentTo<SpawnEntities>(fixture.PlayerA).Count);
    }

    [TestMethod]
    public async Task AnEntityThatNeedsNoSimulationIsGivenToNobody()
    {
        CreatureRespawnEntity respawner = new(crashFish.Transform, 0, "respawn", false, new NitroxId(), NitroxTechType.None, null, null, [], 100f, new NitroxTechType("Peeper"), []);

        await fixture.SpawnByClientAsync(fixture.PlayerA, respawner, false);

        Assert.IsNull(fixture.Ownership.GetPlayerForLock(respawner.Id));
        Assert.AreEqual(0, fixture.Sent<SimulationOwnershipChange>().Count);
    }
}
