using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Test.Server.Helper;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

[TestClass]
public sealed class EntityRepulsedProcessorTest
{
    [TestMethod]
    public async Task TheShotGoesToThePlayerWhoSimulatesTheObject()
    {
        ItemClaimScenario scenario = new();
        WorldEntity item = scenario.AddWorldItem();
        scenario.Ownership.TryAcquire(item.Id, scenario.PlayerB, SimulationLockType.TRANSIENT).Should().BeTrue();

        await new EntityRepulsedProcessor(scenario.Ownership, scenario.EntityRegistry).Process(scenario.Context(scenario.PlayerA), new EntityRepulsed(item.Id, new NitroxVector3(0, 0, 10)));

        scenario.SentTo<EntityRepulsed>(scenario.PlayerB).Should().ContainSingle();
    }

    [TestMethod]
    public async Task AnObjectNobodySimulatesBecomesTheShootersToSimulate()
    {
        ItemClaimScenario scenario = new();
        WorldEntity item = scenario.AddWorldItem();

        await new EntityRepulsedProcessor(scenario.Ownership, scenario.EntityRegistry).Process(scenario.Context(scenario.PlayerA), new EntityRepulsed(item.Id, new NitroxVector3(0, 0, 10)));

        scenario.Ownership.GetPlayerForLock(item.Id).Should().Be(scenario.PlayerA);
        ConnectedPlayers.SentToAll<SimulationOwnershipChange>(scenario.PacketSender).Single().Entities.Single().ChangesPosition.Should().BeTrue();
    }
}
