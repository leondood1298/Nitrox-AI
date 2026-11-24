using System.Reflection;
using NitroxClient.GameLogic.FMOD;
using NitroxClient.MonoBehaviours;
using Nitrox.Model.GameLogic.FMOD;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A stop is never silenced: a sound that should end must end, in this game and in the others.
/// </summary>
public sealed partial class FMOD_StudioEventEmitter_Stop_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((FMOD_StudioEventEmitter t) => t.Stop(default(bool)));

    public static void Postfix(FMOD_StudioEventEmitter __instance, bool allowFadeout)
    {
        if (!__instance.evt.hasHandle() || !__instance.asset || !Resolve<FMODWhitelist>().IsWhitelisted(__instance.asset.path) || FMODSystem.IsOnRemotePlayer(__instance))
        {
            return;
        }

        if (!__instance.TryGetComponentInParent(out NitroxEntity nitroxEntity, true))
        {
            Log.WarnOnce($"[{nameof(FMOD_StudioEventEmitter_Stop_Patch)}] - No NitroxEntity found for {__instance.asset.path} at {__instance.GetFullHierarchyPath()}");
            return;
        }

        Resolve<FMODSystem>().SendStudioEmitterStop(nitroxEntity.Id, __instance.asset.path, allowFadeout);
    }
}
