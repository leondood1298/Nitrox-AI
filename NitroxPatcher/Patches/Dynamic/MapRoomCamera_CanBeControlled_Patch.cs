using System;
using System.CodeDom.Compiler;
using System.Reflection;
using HarmonyLib;
using Nitrox.Model.Helper;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed class MapRoomCamera_CanBeControlled_Patch : NitroxPatch, IDynamicPatch, INitroxPatch
{
    private delegate void PostfixDelegate(MapRoomCamera instance, ref bool result);
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomCamera t) => t.CanBeControlled(null));

    public static void Postfix(MapRoomCamera __instance, ref bool __result)
    {
        __result &= Resolve<MapRoomCameras>().CanSelectForControl(__instance);
    }

    [GeneratedCode("Nitrox.Analyzers", "1.0.13.0")]
    public override void Patch(Harmony harmony)
    {
        PatchMultiple(harmony, TARGET_METHOD, null, new PostfixDelegate(Postfix).Method);
    }
}
