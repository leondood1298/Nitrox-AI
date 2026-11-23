using Nitrox.Model.DataStructures;

namespace NitroxClient.GameLogic;

/// <summary>
///     The game has one time capsule, which takes the id of a rocket's capsule entity. With more than one rocket it stays
///     with the first one it was given, so that items put into it are not filed under another rocket.
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
}
