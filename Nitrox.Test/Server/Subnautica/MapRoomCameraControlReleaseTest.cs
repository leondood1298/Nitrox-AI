using Nitrox.Server.Subnautica.Models.GameLogic;

namespace Nitrox.Test.Server.Subnautica;

[TestClass]
public sealed class MapRoomCameraControlReleaseTest
{
    [TestMethod]
    [DataRow(false, false, true)]
    [DataRow(true, true, true)]
    [DataRow(true, false, false)]
    public void ReleaseIsIdempotentButCannotClearAnotherPlayersLock(bool hasOwner, bool senderOwnsLock, bool expected)
    {
        Assert.AreEqual(expected, ScannerRoomManager.CanAcknowledgeRelease(hasOwner, senderOwnsLock));
    }
}
