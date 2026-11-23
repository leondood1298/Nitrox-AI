using Nitrox.Model.Core;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace NitroxClient.GameLogic.PlayerLogic;

/// <summary>
///     This game stops treating another player's character as its intro partner once that player plays the intro alone.
/// </summary>
[TestClass]
public sealed class PlayerCinematicsTest
{
    private static readonly SessionId partner = 2;
    private static readonly SessionId someoneElse = 3;

    [TestMethod]
    public void ThePartnerPlayingAloneEndsThePartnership()
    {
        Assert.IsTrue(PlayerCinematics.IsPartnerPlayingAlone(partner, partner, IntroCinematicMode.SINGLEPLAYER));
    }

    [TestMethod]
    public void AnotherPlayerPlayingAloneLeavesThePartnerAsItIs()
    {
        Assert.IsFalse(PlayerCinematics.IsPartnerPlayingAlone(partner, someoneElse, IntroCinematicMode.SINGLEPLAYER));
        Assert.IsFalse(PlayerCinematics.IsPartnerPlayingAlone(null, partner, IntroCinematicMode.SINGLEPLAYER));
    }

    [TestMethod]
    [DataRow(IntroCinematicMode.PLAYING)]
    [DataRow(IntroCinematicMode.COMPLETED)]
    public void ThePartnersOtherStepsLeaveThePartnership(IntroCinematicMode mode)
    {
        // Its intro ending is handled where this game's intro ends
        Assert.IsFalse(PlayerCinematics.IsPartnerPlayingAlone(partner, partner, mode));
    }
}
