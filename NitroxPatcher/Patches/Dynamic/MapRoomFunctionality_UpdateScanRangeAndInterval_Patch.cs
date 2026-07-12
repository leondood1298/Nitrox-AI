using System;
using System.CodeDom.Compiler;
using System.Reflection;
using HarmonyLib;
using Nitrox.Model.Helper;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed class MapRoomFunctionality_UpdateScanRangeAndInterval_Patch : NitroxPatch, IDynamicPatch, INitroxPatch
{
    private delegate void PrefixDelegate(MapRoomFunctionality __instance, out float __state);

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

    [GeneratedCode("Nitrox.Analyzers", "1.0.13.0")]
    public override void Patch(Harmony harmony)
    {
        PatchMultiple(harmony, TARGET_METHOD, new PrefixDelegate(Prefix).Method, new Action<MapRoomFunctionality, float>(Postfix).Method);
    }
}
