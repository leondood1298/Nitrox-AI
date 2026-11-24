using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Test.Server.GameLogic.Bases;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

[TestClass]
public sealed class BulkheadDoorRegisteredProcessorTest
{
    private static readonly NitroxBaseFace face = new(new NitroxInt3(1, 0, 2), 3);

    [TestMethod]
    public async Task DoorIsKeptAsAChildOfItsBaseWithItsState()
    {
        BuildingScenarioFixture scenario = new();
        BulkheadDoorEntity door = new(new NitroxId("00000000-0000-0000-0000-000000000a01"), BuildingScenarioFixture.BaseId, face, new BulkheadDoorMetadata(true));

        await Processor(scenario).Process(scenario.Context(scenario.PlayerA), new BulkheadDoorRegistered(door));

        Assert.AreSame(door, scenario.Base.ChildEntities.OfType<BulkheadDoorEntity>().Single());
        Assert.IsTrue(((BulkheadDoorMetadata)door.Metadata).Opened);
    }

    /// <summary>
    ///     Two players open the same door for the first time at once: the first registration wins and the second player is
    ///     told which door entity to use.
    /// </summary>
    [TestMethod]
    public async Task SecondRegistrationOfADoorIsAnsweredWithTheFirst()
    {
        BuildingScenarioFixture scenario = new();
        BulkheadDoorEntity first = new(new NitroxId("00000000-0000-0000-0000-000000000a01"), BuildingScenarioFixture.BaseId, face, new BulkheadDoorMetadata(true));
        BulkheadDoorEntity second = new(new NitroxId("00000000-0000-0000-0000-000000000a02"), BuildingScenarioFixture.BaseId, face, new BulkheadDoorMetadata(false));
        await Processor(scenario).Process(scenario.Context(scenario.PlayerA), new BulkheadDoorRegistered(first));

        await Processor(scenario).Process(scenario.Context(scenario.PlayerB), new BulkheadDoorRegistered(second));

        Assert.AreSame(first, scenario.Base.ChildEntities.OfType<BulkheadDoorEntity>().Single());
        Assert.AreSame(first, scenario.SentTo<BulkheadDoorRegistered>(scenario.PlayerB).Single().Entity);
    }

    private static BulkheadDoorRegisteredProcessor Processor(BuildingScenarioFixture scenario) =>
        new(scenario.EntityRegistry, scenario.WorldEntityManager, NullLogger<BulkheadDoorRegisteredProcessor>.Instance);
}
