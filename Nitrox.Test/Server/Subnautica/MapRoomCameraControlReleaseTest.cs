using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nitrox.Server.Subnautica.Models.Packets.Processors;

namespace Nitrox.Test.Server.Subnautica;

[TestClass]
public sealed class MapRoomCameraControlReleaseTest
{
    [DataTestMethod]
    [DataRow(false, false, true)]
    [DataRow(true, true, true)]
    [DataRow(true, false, false)]
    public void ReleaseIsIdempotentButCannotClearAnotherPlayersLock(bool hasOwner, bool senderOwnsLock, bool expected)
    {
        Assert.AreEqual(expected, MapRoomCameraControlProcessor.CanAcknowledgeRelease(hasOwner, senderOwnsLock));
    }
}
