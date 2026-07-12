using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nitrox.Model.DataStructures;
using Nitrox.Server.Subnautica.Models.GameLogic.Bases;

namespace Nitrox.Test.Server.GameLogic.Bases;

[TestClass]
public sealed class MapRoomTopologyAuthorityTest
{
    [TestMethod]
    public void AllowsExistingBaseChild()
    {
        NitroxId baseId = new();
        Assert.IsTrue(MapRoomTopologyAuthority.IsAllowedParent(baseId, baseId, (null, null)));
    }

    [TestMethod]
    public void AllowsChildFromBaseBeingMergedIntoTarget()
    {
        NitroxId source = new();
        NitroxId target = new();
        Assert.IsTrue(MapRoomTopologyAuthority.IsAllowedParent(source, target, (source, target)));
    }

    [TestMethod]
    public void RejectsUnrelatedOrReverseTransferParent()
    {
        NitroxId source = new();
        NitroxId target = new();
        NitroxId unrelated = new();

        Assert.IsFalse(MapRoomTopologyAuthority.IsAllowedParent(unrelated, target, (source, target)));
        Assert.IsFalse(MapRoomTopologyAuthority.IsAllowedParent(source, target, (target, source)));
        Assert.IsFalse(MapRoomTopologyAuthority.IsAllowedParent(null, target, (source, target)));
    }
}
