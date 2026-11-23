using System;
using System.Reflection;
using Nitrox.Model.Helper;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class FireExtinguisher_Update_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((FireExtinguisher t) => t.Update());

    private static bool localWasSpraying;

    public static bool Prefix(FireExtinguisher __instance)
    {
        if (Player.main)
        {
            return ((PlayerTool)__instance).usingPlayer == Player.main;
        }
        return false;
    }

    public static void Postfix(FireExtinguisher __instance)
    {
        if (Player.main && ((PlayerTool)__instance).usingPlayer == Player.main && __instance.fxIsPlaying != localWasSpraying)
        {
            localWasSpraying = __instance.fxIsPlaying;
            Resolve<LocalPlayer>().BroadcastFireExtinguisherUse(__instance.fxIsPlaying);
        }
    }
}
