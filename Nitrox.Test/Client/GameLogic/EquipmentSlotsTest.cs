using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class EquipmentSlotsTest
{
    [TestMethod]
    public void WhatThePlayerPutsOnOrTakesOffInARunningGameIsReported()
    {
        EquipmentSlots.IsPlayersOwnChange(true, true).Should().BeTrue();
    }

    [TestMethod]
    public void WhatIsPutOnWhileThePlayerIsJoiningIsNotReported()
    {
        EquipmentSlots.IsPlayersOwnChange(true, false).Should().BeFalse();
    }

    [TestMethod]
    public void WhatIsTakenOffWhileTheGameIsTornDownAfterLeavingIsNotReported()
    {
        // Leaving disconnects first; the destroyed items then leave their slots
        EquipmentSlots.IsPlayersOwnChange(false, true).Should().BeFalse();
    }
}
