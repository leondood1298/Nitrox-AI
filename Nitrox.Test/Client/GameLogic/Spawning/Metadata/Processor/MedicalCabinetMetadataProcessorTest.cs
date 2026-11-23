using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using DoorChange = NitroxClient.GameLogic.Spawning.Metadata.Processor.MedicalCabinetMetadataProcessor.DoorChange;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

/// <summary>
///     What a medical cabinet's door does when another player's state for it arrives, or when a joiner gets the stored
///     state. The game closes the door two seconds after its medkit is taken; the cabinet state already counts that door
///     as closed.
/// </summary>
[TestClass]
public sealed class MedicalCabinetMetadataProcessorTest
{
    private const float NEXT_MEDKIT = 1200f;

    [TestMethod]
    public void ADoorThatIsAsTheStateSaysIsLeftAlone()
    {
        Assert.AreEqual(DoorChange.NONE, MedicalCabinetMetadataProcessor.GetDoorChange(false, false, true, new MedicalCabinetMetadata(false, true, NEXT_MEDKIT)));
        Assert.AreEqual(DoorChange.NONE, MedicalCabinetMetadataProcessor.GetDoorChange(true, false, true, new MedicalCabinetMetadata(true, true, NEXT_MEDKIT)));
    }

    [TestMethod]
    public void ADoorAnotherPlayerOpenedOpensAtOnce()
    {
        Assert.AreEqual(DoorChange.TOGGLE_NOW, MedicalCabinetMetadataProcessor.GetDoorChange(false, false, true, new MedicalCabinetMetadata(true, true, NEXT_MEDKIT)));
    }

    [TestMethod]
    public void ADoorAJoinerFindsOpenOpensAtOnce()
    {
        // A freshly loaded cabinet is closed and holds whatever its prefab holds.
        Assert.AreEqual(DoorChange.TOGGLE_NOW, MedicalCabinetMetadataProcessor.GetDoorChange(false, false, false, new MedicalCabinetMetadata(true, false, NEXT_MEDKIT)));
    }

    [TestMethod]
    public void ADoorAnotherPlayerClosedWithoutTakingTheMedkitClosesAtOnce()
    {
        Assert.AreEqual(DoorChange.TOGGLE_NOW, MedicalCabinetMetadataProcessor.GetDoorChange(true, false, true, new MedicalCabinetMetadata(false, true, NEXT_MEDKIT)));
    }

    [TestMethod]
    public void ADoorWhoseMedkitAnotherPlayerTookClosesAsItDoesForThatPlayer()
    {
        Assert.AreEqual(DoorChange.TOGGLE_AFTER_PICKUP, MedicalCabinetMetadataProcessor.GetDoorChange(true, false, true, new MedicalCabinetMetadata(false, false, NEXT_MEDKIT)));
    }

    [TestMethod]
    public void ADoorThatIsAboutToCloseIsLeftToClose()
    {
        // This player took the medkit itself a moment ago, and another player's game reports the closed door.
        Assert.AreEqual(DoorChange.NONE, MedicalCabinetMetadataProcessor.GetDoorChange(true, true, false, new MedicalCabinetMetadata(false, false, NEXT_MEDKIT)));
    }

    [TestMethod]
    public void ADoorThatIsAboutToCloseButShouldStayOpenStaysOpen()
    {
        Assert.AreEqual(DoorChange.CANCEL_PENDING_TOGGLE, MedicalCabinetMetadataProcessor.GetDoorChange(true, true, false, new MedicalCabinetMetadata(true, true, NEXT_MEDKIT)));
    }
}
