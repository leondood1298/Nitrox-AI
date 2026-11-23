using NitroxPatcher.Patches.Dynamic;

namespace Nitrox.Test.Patcher.Patches.Dynamic;

[TestClass]
public sealed class MapRoomCameraDocking_Start_PatchTest
{
    [TestMethod]
    public void GameNeverGivesADockACameraOfItsOwn()
    {
        // The cameras of a room are entities the server sends. A dock that made one itself would end up with two.
        Assert.IsFalse(MapRoomCameraDocking_Start_Patch.Prefix());
    }
}
