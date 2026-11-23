using Nitrox.Server.Subnautica.Models.Packets.Processors;

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
        Assert.AreEqual(expected, MapRoomCameraControlProcessor.CanAcknowledgeRelease(hasOwner, senderOwnsLock));
    }

    [TestMethod]
    [DataRow(true, 0, false, true)]
    [DataRow(false, 0, false, false)]
    [DataRow(true, 1, false, true)]
    [DataRow(false, 1, false, true)]
    [DataRow(true, 1, true, false)]
    [DataRow(true, 2, false, false)]
    public void LooseControlRequiresKnownWorldCameraOrOneUndockedRegistration(bool validWorldCamera, int registrations, bool docked, bool expected)
    {
        Assert.AreEqual(expected, MapRoomCameraControlProcessor.IsValidLooseAssociation(validWorldCamera, registrations, docked));
    }
}
