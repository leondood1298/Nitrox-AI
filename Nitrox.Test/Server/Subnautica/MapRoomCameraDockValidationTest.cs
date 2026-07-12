using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nitrox.Server.Subnautica.Models.Packets.Processors;

namespace Nitrox.Test.Server.Subnautica;

[TestClass]
public sealed class MapRoomCameraDockValidationTest
{
    [DataTestMethod]
    [DataRow(true, false, true)]
    [DataRow(false, true, true)]
    [DataRow(true, true, true)]
    [DataRow(false, false, false)]
    public void AcceptsWorldOrRestoredRegisteredCamera(bool validWorldCamera, bool registeredCamera, bool expected)
    {
        Assert.AreEqual(expected, MapRoomCameraDockProcessor.IsKnownCamera(validWorldCamera, registeredCamera));
    }
}
