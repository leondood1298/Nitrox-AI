using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

[TestClass]
public sealed class FiltrationMachineMetadataAuthorityTest
{
    private const float IDLE = FiltrationMachineMetadata.IDLE;

    [TestMethod]
    public void TimersFromTheSimulatingPlayerAreTaken()
    {
        Assert.IsTrue(FiltrationMachineMetadataAuthority.TryAccept(CreateMachine(), new FiltrationMachineMetadata(600f, IDLE), true, out FiltrationMachineMetadata accepted));

        Assert.AreEqual(600f, accepted.TimeRemainingWater);
        Assert.AreEqual(IDLE, accepted.TimeRemainingSalt);
    }

    [TestMethod]
    public void TimersFromAnyoneElseAreNotTaken()
    {
        Assert.IsFalse(FiltrationMachineMetadataAuthority.TryAccept(CreateMachine(), new FiltrationMachineMetadata(600f, IDLE), false, out _));
    }

    [TestMethod]
    [DataRow(float.NaN, IDLE)]
    [DataRow(IDLE, float.PositiveInfinity)]
    [DataRow(FiltrationMachineMetadata.WATER_SECONDS + 1f, IDLE)]
    [DataRow(IDLE, FiltrationMachineMetadata.SALT_SECONDS + 1f)]
    public void TimersTheMachineCannotHaveAreNotTaken(float water, float salt)
    {
        Assert.IsFalse(FiltrationMachineMetadataAuthority.TryAccept(CreateMachine(), new FiltrationMachineMetadata(water, salt), true, out _));
    }

    [TestMethod]
    public void StoppedTimerIsStoredAsTheGameStopsIt()
    {
        Assert.IsTrue(FiltrationMachineMetadataAuthority.TryAccept(CreateMachine(), new FiltrationMachineMetadata(0f, -7f), true, out FiltrationMachineMetadata accepted));

        Assert.AreEqual(IDLE, accepted.TimeRemainingWater);
        Assert.AreEqual(IDLE, accepted.TimeRemainingSalt);
    }

    [TestMethod]
    public void TimersAreNotTakenForAnotherKindOfEntity()
    {
        InteriorPieceEntity bioreactor = CreateMachine();
        bioreactor.TechType = new NitroxTechType("BaseBioReactor");

        Assert.IsFalse(FiltrationMachineMetadataAuthority.TryAccept(bioreactor, new FiltrationMachineMetadata(600f, IDLE), true, out _));
    }

    private static InteriorPieceEntity CreateMachine()
    {
        InteriorPieceEntity machine = InteriorPieceEntity.MakeEmpty();
        machine.Id = new NitroxId();
        machine.TechType = new NitroxTechType("BaseFiltrationMachine");
        return machine;
    }
}
