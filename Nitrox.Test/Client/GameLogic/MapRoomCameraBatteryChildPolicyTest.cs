using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic;

namespace Nitrox.Test.Client.GameLogic;

[TestClass]
public sealed class MapRoomCameraBatteryChildPolicyTest
{
    [TestMethod]
    public void ExcludesOnlyMapRoomCameraBatteryMetadata()
    {
        BatteryMetadata battery = new(99f);
        PowerSourceMetadata powerSource = new(50f, 75f, BasePowerSourceType.SOLAR, 1);

        Assert.IsTrue(MapRoomCameraBatteryChildPolicy.IsRedundant(true, battery));
        Assert.IsFalse(MapRoomCameraBatteryChildPolicy.IsRedundant(false, battery));
        Assert.IsFalse(MapRoomCameraBatteryChildPolicy.IsRedundant(true, powerSource));
        Assert.IsFalse(MapRoomCameraBatteryChildPolicy.IsRedundant(true, null));
    }
}
