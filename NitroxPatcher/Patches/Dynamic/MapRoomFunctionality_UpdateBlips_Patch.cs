using System;
using System.CodeDom.Compiler;
using System.Reflection;
using HarmonyLib;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Helper;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed class MapRoomFunctionality_UpdateBlips_Patch : NitroxPatch, IDynamicPatch, INitroxPatch
{
    private delegate void PrefixDelegate(MapRoomFunctionality __instance, out int __state);

    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomFunctionality t) => t.UpdateBlips());

    public static void Prefix(MapRoomFunctionality __instance, out int __state)
    {
        __state = __instance.numNodesScanned;
    }

    public static void Postfix(MapRoomFunctionality __instance, int __state)
    {
        if (__instance.numNodesScanned != __state && __instance.TryGetNitroxId(out NitroxId roomId) && Resolve<SimulationOwnership>().HasAnyLockType(roomId))
        {
            Resolve<Entities>().EntityMetadataChangedThrottled(__instance, roomId);
        }
    }

    [GeneratedCode("Nitrox.Analyzers", "1.0.13.0")]
    public override void Patch(Harmony harmony)
    {
        PatchMultiple(harmony, TARGET_METHOD, new PrefixDelegate(Prefix).Method, new Action<MapRoomFunctionality, int>(Postfix).Method);
    }
}
