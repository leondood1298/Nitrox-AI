using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class uGUI_MapRoomScanner_OnDisable_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((uGUI_MapRoomScanner t) => t.OnDisable());

    public static void Postfix(uGUI_MapRoomScanner __instance) => Resolve<MapRoomScanResultSubscriber>().Set(__instance, false);
}
