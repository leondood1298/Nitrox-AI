using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class MapRoomFunctionality_UpdateScanRangeAndInterval_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomFunctionality t) => t.UpdateScanRangeAndInterval());

    public static void Postfix(MapRoomFunctionality __instance)
    {
        __instance.scanRange = MapRoomUpgradeEffects.ScanRange(__instance.storageContainer.container.GetCount(TechType.MapRoomUpgradeScanRange));
        __instance.scanInterval = MapRoomUpgradeEffects.ScanInterval(__instance.storageContainer.container.GetCount(TechType.MapRoomUpgradeScanSpeed));
    }
}
