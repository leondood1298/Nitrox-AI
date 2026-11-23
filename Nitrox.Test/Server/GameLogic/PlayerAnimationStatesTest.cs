using Nitrox.Model.Core;
using Nitrox.Model.GameLogic.PlayerAnimation;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;

namespace Nitrox.Test.Server.GameLogic;

[TestClass]
public sealed class PlayerAnimationStatesTest
{
    [TestMethod]
    public void JoiningPlayerIsToldWhoIsHoldingUpTheirPdaButNotAboutThemselves()
    {
        PlayerAnimationStates states = new();
        SessionId reader = 1;
        SessionId joiner = 2;
        states.Set(reader, new PlayerAnimation(AnimChangeType.USING_PDA, AnimChangeState.ON));

        AnimationChangeEvent told = states.GetAnnouncementsFor(joiner).Single();

        Assert.AreEqual(reader, told.SessionId);
        Assert.AreEqual(AnimChangeType.USING_PDA, told.Animation.Type);
        Assert.AreEqual(AnimChangeState.ON, told.Animation.State);
        Assert.AreEqual(0, states.GetAnnouncementsFor(reader).Count);
    }

    [TestMethod]
    public void PuttingThePdaAwayStopsTheAnnouncement()
    {
        PlayerAnimationStates states = new();
        states.Set(1, new PlayerAnimation(AnimChangeType.USING_PDA, AnimChangeState.ON));
        states.Set(1, new PlayerAnimation(AnimChangeType.USING_TOOL, AnimChangeState.ON));

        states.Set(1, new PlayerAnimation(AnimChangeType.USING_PDA, AnimChangeState.OFF));

        Assert.AreEqual(AnimChangeType.USING_TOOL, states.GetAnnouncementsFor(2).Single().Animation.Type);
    }

    [TestMethod]
    public void MomentaryAnimationsAreNotRepeatedToJoiningPlayers()
    {
        // A player who joins seconds after someone jumped or died must not see that happen again.
        PlayerAnimationStates states = new();
        foreach (AnimChangeType type in Enum.GetValues<AnimChangeType>())
        {
            if (!PlayerAnimationStates.IsLasting(type))
            {
                states.Set(1, new PlayerAnimation(type, AnimChangeState.ON));
            }
        }

        Assert.AreEqual(0, states.GetAnnouncementsFor(2).Count);
    }
}
