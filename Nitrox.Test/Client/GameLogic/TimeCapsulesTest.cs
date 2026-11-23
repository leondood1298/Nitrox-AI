using Nitrox.Model.DataStructures;

namespace NitroxClient.GameLogic;

/// <summary>
///     The game has one time capsule, which takes the id of a rocket's capsule entity. A spawn of another rocket's capsule
///     does not take it over, so that items put into it are not filed under another rocket; opening it from another rocket's
///     cockpit swaps it to that rocket's capsule. A rocket whose capsule is not shown answers its preflight check from what
///     its capsule held when last known.
/// </summary>
[TestClass]
public sealed class TimeCapsulesTest
{
    [TestMethod]
    public void CapsuleWithoutIdTakesTheRocketsCapsule()
    {
        TimeCapsules.CanTakeCapsule(null, new NitroxId()).Should().BeTrue();
    }

    [TestMethod]
    public void CapsuleTakesTheSameRocketsCapsuleAgain()
    {
        NitroxId capsuleId = new();

        TimeCapsules.CanTakeCapsule(new NitroxId(capsuleId.ToString()), capsuleId).Should().BeTrue();
    }

    [TestMethod]
    public void CapsuleOfAnotherRocketDoesNotTakeOverTheCapsule()
    {
        TimeCapsules.CanTakeCapsule(new NitroxId(), new NitroxId()).Should().BeFalse();
    }

    [TestMethod]
    public void OpeningTheRocketWhoseCapsuleIsShownSwapsNothing()
    {
        NitroxId capsuleId = new();

        TimeCapsules.MustSwap(new NitroxId(capsuleId.ToString()), capsuleId).Should().BeFalse();
    }

    [TestMethod]
    public void OpeningAnotherRocketsCapsuleSwapsTheCapsule()
    {
        TimeCapsules.MustSwap(new NitroxId(), new NitroxId()).Should().BeTrue();
    }

    [TestMethod]
    public void CapsuleShowingNoRocketsCapsuleIsSwappedOnOpening()
    {
        TimeCapsules.MustSwap(null, new NitroxId()).Should().BeTrue();
    }

    [TestMethod]
    public void PutAwayCapsuleIsReadyOnlyWithTextPictureAndItems()
    {
        TimeCapsules.IsReady(true, true, true).Should().BeTrue();
        TimeCapsules.IsReady(false, true, true).Should().BeFalse();
        TimeCapsules.IsReady(true, false, true).Should().BeFalse();
        TimeCapsules.IsReady(true, true, false).Should().BeFalse();
    }
}
