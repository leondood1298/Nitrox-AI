using System;
using System.CodeDom.Compiler;
using System.Reflection;
using HarmonyLib;
using Nitrox.Model.Helper;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed class MapRoomFunctionality_OnResourceDiscovered_Patch : NitroxPatch, IDynamicPatch, INitroxPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomFunctionality t) => t.OnResourceDiscovered(null));

    public static bool Prefix(MapRoomFunctionality __instance) =>
        Resolve<MapRoomScanResultBroadcaster>().ShouldRunVanillaResults(__instance);

    public static void Postfix(MapRoomFunctionality __instance, ResourceTrackerDatabase.ResourceInfo info)
    {
        Resolve<MapRoomScanResultBroadcaster>().BroadcastDiscovered(__instance, info);
    }

    [GeneratedCode("Nitrox.Analyzers", "1.0.13.0")]
    public override void Patch(Harmony harmony)
    {
        PatchMultiple(harmony, TARGET_METHOD, new Func<MapRoomFunctionality, bool>(Prefix).Method,
            new Action<MapRoomFunctionality, ResourceTrackerDatabase.ResourceInfo>(Postfix).Method);
    }
}
