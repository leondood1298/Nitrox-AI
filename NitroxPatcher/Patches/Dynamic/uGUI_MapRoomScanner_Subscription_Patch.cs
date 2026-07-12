using System;
using System.CodeDom.Compiler;
using System.Reflection;
using HarmonyLib;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed class uGUI_MapRoomScanner_Subscription_Patch : NitroxPatch, IDynamicPatch, INitroxPatch
{
    private static readonly MethodInfo ON_ENABLE = AccessTools.Method(typeof(uGUI_MapRoomScanner), "OnEnable");
    private static readonly MethodInfo ON_DISABLE = AccessTools.Method(typeof(uGUI_MapRoomScanner), "OnDisable");

    public static void Enabled(uGUI_MapRoomScanner __instance) => Resolve<MapRoomScanResultSubscriber>().Set(__instance, true);
    public static void Disabled(uGUI_MapRoomScanner __instance) => Resolve<MapRoomScanResultSubscriber>().Set(__instance, false);

    [GeneratedCode("Nitrox.Analyzers", "1.0.13.0")]
    public override void Patch(Harmony harmony)
    {
        PatchMultiple(harmony, ON_ENABLE, null, new Action<uGUI_MapRoomScanner>(Enabled).Method);
        PatchMultiple(harmony, ON_DISABLE, null, new Action<uGUI_MapRoomScanner>(Disabled).Method);
    }
}
