using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class MapRoomFunctionality_UpdateScanRangeAndInterval_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomFunctionality t) => t.UpdateScanRangeAndInterval());

    public static void Prefix(MapRoomFunctionality __instance, out float __state)
    {
        __state = __instance.GetScanRange();
    }

    public static void Postfix(MapRoomFunctionality __instance, float __state)
    {
        __instance.scanRange = MapRoomUpgradeEffects.ScanRange(__instance.storageContainer.container.GetCount(TechType.MapRoomUpgradeScanRange));
        __instance.scanInterval = MapRoomUpgradeEffects.ScanInterval(__instance.storageContainer.container.GetCount(TechType.MapRoomUpgradeScanSpeed));

        if (__instance.typeToScan != TechType.None && __instance.GetScanRange() != __state)
        {
            Resolve<MapRoomScanResultBroadcaster>().BroadcastSnapshot(__instance);
        }
    }
}
