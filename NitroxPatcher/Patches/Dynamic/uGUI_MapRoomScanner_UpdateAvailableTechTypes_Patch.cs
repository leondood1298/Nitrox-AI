using System;
using System.CodeDom.Compiler;
using System.Reflection;
using HarmonyLib;
using Nitrox.Model.Helper;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed class uGUI_MapRoomScanner_UpdateAvailableTechTypes_Patch : NitroxPatch, IDynamicPatch, INitroxPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((uGUI_MapRoomScanner t) => t.UpdateAvailableTechTypes());

    public static bool Prefix(uGUI_MapRoomScanner __instance) => Resolve<MapRoomScanTypes>().ShouldRunVanilla(__instance);

    public static void Postfix(uGUI_MapRoomScanner __instance) => Resolve<MapRoomScanTypes>().Publish(__instance);

    [GeneratedCode("Nitrox.Analyzers", "1.0.13.0")]
    public override void Patch(Harmony harmony)
    {
        PatchMultiple(harmony, TARGET_METHOD, new Func<uGUI_MapRoomScanner, bool>(Prefix).Method, new Action<uGUI_MapRoomScanner>(Postfix).Method);
    }
}
