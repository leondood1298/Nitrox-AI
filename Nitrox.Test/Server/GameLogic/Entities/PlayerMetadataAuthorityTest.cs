namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

[TestClass]
public sealed class PlayerMetadataAuthorityTest
{
    [TestMethod]
    public void EquipmentAPlayerChangesInARunningGameIsAccepted()
    {
        PlayerMetadataAuthority.Judge(true, true, true, true).Should().Be(PlayerMetadataVerdict.ACCEPT);
    }

    [TestMethod]
    public void EquipmentSentWhileThePlayerIsStillJoiningIsRefused()
    {
        // Rejoin 1 of the live test: the game emptied the list while it put the joining player's tank on
        PlayerMetadataAuthority.Judge(true, true, true, false).Should().Be(PlayerMetadataVerdict.NOT_JOINED_YET);
    }

    [TestMethod]
    public void EquipmentForAnotherPlayersBodyIsRefused()
    {
        PlayerMetadataAuthority.Judge(true, true, false, true).Should().Be(PlayerMetadataVerdict.OTHER_PLAYERS_BODY);
    }

    [TestMethod]
    public void OtherMetadataCannotTakeThePlaceOfABodysEquipmentList()
    {
        // The lungs' oxygen of a damage report used to be stored on the body
        PlayerMetadataAuthority.Judge(true, false, true, true).Should().Be(PlayerMetadataVerdict.NOT_EQUIPMENT);
        PlayerMetadataAuthority.Judge(true, false, false, true).Should().Be(PlayerMetadataVerdict.NOT_EQUIPMENT);
    }

    [TestMethod]
    public void EquipmentListForSomethingThatIsNoPlayerIsRefused()
    {
        PlayerMetadataAuthority.Judge(false, true, false, true).Should().Be(PlayerMetadataVerdict.NOT_ON_A_PLAYER);
    }

    [TestMethod]
    public void OtherMetadataForOtherEntitiesIsLeftToTheOtherRules()
    {
        PlayerMetadataAuthority.Judge(false, false, false, false).Should().Be(PlayerMetadataVerdict.NOT_PLAYER_STATE);
    }
}
