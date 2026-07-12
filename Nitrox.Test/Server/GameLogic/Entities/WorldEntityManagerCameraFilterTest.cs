using Nitrox.Model.DataStructures;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Test.Server.GameLogic.Entities;

[TestClass]
public sealed class WorldEntityManagerCameraFilterTest
{
    [TestMethod]
    public void InitialSyncExcludesDockedCameraEntityButKeepsLooseEntity()
    {
        NitroxId docked = new();
        NitroxId loose = new();
        HashSet<NitroxId> dockedIds = [docked];

        Assert.IsFalse(WorldEntityManager.ShouldIncludeInInitialSync(docked, dockedIds));
        Assert.IsTrue(WorldEntityManager.ShouldIncludeInInitialSync(loose, dockedIds));
    }
}
