using Microsoft.VisualStudio.TestTools.UnitTesting;
using NitroxPatcher.Patches.Dynamic;

namespace Nitrox.Test.Patcher.Patches.Dynamic;

[TestClass]
public sealed class MapRoomFunctionality_UpdateScanning_PatchTest
{
    [DataTestMethod]
    [DataRow(false, false, false)]
    [DataRow(true, true, false)]
    [DataRow(true, false, true)]
    public void SuppressesOnlyKnownNonOwnerDrain(bool hasEntityId, bool hasOwnership, bool expected)
    {
        Assert.AreEqual(expected, MapRoomFunctionality_UpdateScanning_Patch.ShouldSuppressPowerDrain(hasEntityId, hasOwnership));
    }
}
