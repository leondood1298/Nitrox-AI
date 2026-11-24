using System.Reflection;
using NitroxClient.GameLogic.FMOD;
using Nitrox.Model.GameLogic.FMOD;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class FMOD_StudioEventEmitter_PlayUI_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((FMOD_StudioEventEmitter t) => t.PlayUI());

    public static bool Prefix()
    {
        return !FMODSoundSuppressor.SuppressFMODEvents;
    }

    public static void Postfix(FMOD_StudioEventEmitter __instance, bool __result)
    {
        // Only run if evt was started in original method (false as well when the prefix silenced it)
        if (__result)
        {
            Resolve<FMODSystem>().SendStudioEmitterStarted(__instance, Resolve<FMODWhitelist>());
        }
    }
}
