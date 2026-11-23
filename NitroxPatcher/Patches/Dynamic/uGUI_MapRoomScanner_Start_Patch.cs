using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class uGUI_MapRoomScanner_Start_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((uGUI_MapRoomScanner t) => t.Start());

    public static void Postfix(uGUI_MapRoomScanner __instance) => Resolve<MapRoomScanResultSubscriber>().Set(__instance, true);
}
