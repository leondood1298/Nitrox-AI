using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

/// <summary>
///     The modes the server gives this game's player for the intro.
/// </summary>
[TestClass]
public sealed class SetIntroCinematicModeProcessorTest
{
    [TestMethod]
    public void AGameThatWaitsForAPartnerStartsWhenThePairingSaysSo()
    {
        Assert.IsTrue(SetIntroCinematicModeProcessor.TakesMode(IntroCinematicMode.WAITING, IntroCinematicMode.START));
    }

    [TestMethod]
    [DataRow(IntroCinematicMode.SINGLEPLAYER)]
    [DataRow(IntroCinematicMode.COMPLETED)]
    public void AGameWhosePlayerChoseToPlayAloneDoesNotStartWithAPartner(IntroCinematicMode current)
    {
        // The server paired the player at the moment it chose to play alone, and lets the partner wait again
        Assert.IsFalse(SetIntroCinematicModeProcessor.TakesMode(current, IntroCinematicMode.START));
    }
}
