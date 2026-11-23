using System;
using System.Reflection;
using HarmonyLib;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed class uGUI_MapRoomScanner_Subscription_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD_START = Reflect.Method((uGUI_MapRoomScanner t) => t.Start());
    private static readonly MethodInfo TARGET_METHOD_ON_DISABLE = Reflect.Method((uGUI_MapRoomScanner t) => t.OnDisable());

    public static void PostfixStart(uGUI_MapRoomScanner __instance) => Resolve<MapRoomScanResultSubscriber>().Set(__instance, true);

    public static void PostfixOnDisable(uGUI_MapRoomScanner __instance) => Resolve<MapRoomScanResultSubscriber>().Set(__instance, false);

    public override void Patch(Harmony harmony)
    {
        PatchPostfix(harmony, TARGET_METHOD_START, ((Action<uGUI_MapRoomScanner>)PostfixStart).Method);
        PatchPostfix(harmony, TARGET_METHOD_ON_DISABLE, ((Action<uGUI_MapRoomScanner>)PostfixOnDisable).Method);
    }
}
