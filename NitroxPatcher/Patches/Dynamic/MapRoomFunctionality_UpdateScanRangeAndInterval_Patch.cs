using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class MapRoomFunctionality_UpdateScanRangeAndInterval_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomFunctionality t) => t.UpdateScanRangeAndInterval());

    public static void Postfix(MapRoomFunctionality __instance) => MapRoomUpgradeEffects.Apply(__instance);
}
