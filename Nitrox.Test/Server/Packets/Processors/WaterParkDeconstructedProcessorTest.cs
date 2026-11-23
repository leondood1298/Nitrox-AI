using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Test.Server.GameLogic.Bases;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

[TestClass]
public sealed class WaterParkDeconstructedProcessorTest
{
    private static readonly NitroxId middlePieceId = new("00000000-0000-0000-0000-000000000901");
    private static readonly NitroxId splitOffId = new("00000000-0000-0000-0000-000000000902");

    /// <summary>
    ///     Removing the middle of a three-high containment splits off a new one. Fish are only born, and eggs only hatch,
    ///     in a containment somebody simulates.
    /// </summary>
    [TestMethod]
    public async Task SplitOffContainmentIsSimulatedByThePlayerWhoSplitIt()
    {
        BuildingScenarioFixture scenario = new();
        scenario.AddInteriorPiece(middlePieceId, "BaseWaterPark");
        InteriorPieceEntity splitOff = InteriorPieceEntity.MakeEmpty();
        splitOff.Id = splitOffId;
        splitOff.ParentId = BuildingScenarioFixture.BaseId;
        WaterParkDeconstructed packet = new(BuildingScenarioFixture.BaseId, middlePieceId, default, BuildingScenarioFixture.Ghost(middlePieceId), new BaseData(), splitOff,
            [], false, 1);

        await new WaterParkDeconstructedProcessor(scenario.BuildingManager, scenario.EntitySimulation).Process(scenario.Context(scenario.PlayerA), packet);

        Assert.IsTrue(scenario.EntityRegistry.TryGetEntityById(splitOffId, out InteriorPieceEntity _));
        Assert.AreSame(scenario.PlayerA, scenario.Ownership.GetPlayerForLock(splitOffId));
    }
}
