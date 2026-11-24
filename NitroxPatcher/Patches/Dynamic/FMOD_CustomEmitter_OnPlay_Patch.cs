using System.Reflection;
using FMOD.Studio;
using NitroxClient.GameLogic.FMOD;
using NitroxClient.MonoBehaviours;
using Nitrox.Model.GameLogic.FMOD;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class FMOD_CustomEmitter_OnPlay_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((FMOD_CustomEmitter t) => t.OnPlay());

    public static void Postfix(FMOD_CustomEmitter __instance)
    {
        if (!__instance.asset || !Resolve<FMODWhitelist>().IsWhitelisted(__instance.asset.path) || FMODSystem.IsOnRemotePlayer(__instance))
        {
            return;
        }

        string path = __instance.asset.path;
        EventInstance evt = __instance.GetEventInstance();
        evt.getVolume(out float volume, out float _);
        FMODSystem.EventKind kind = FMODSystem.GetEventKind(path);
        bool looping = !kind.IsOneshot;

        if (__instance.TryGetComponentInParent(out NitroxEntity nitroxEntity, true))
        {
            if (kind.Is3D)
            {
                Resolve<FMODSystem>().SendCustomEmitterPlay(nitroxEntity.Id, path, looping);
            }
            else
            {
                Resolve<FMODSystem>().SendEventInstancePlay(nitroxEntity.Id, path, __instance.transform.position.ToDto(), volume, looping);
            }
        }
        else if (!looping)
        {
            Resolve<FMODSystem>().SendAssetPlay(path, __instance.transform.position.ToDto(), volume);
        }
        else
        {
            // Without an entity there is nothing to stop it on: sent as a one-shot it would loop forever for the others
            Log.WarnOnce($"[{nameof(FMOD_CustomEmitter_OnPlay_Patch)}] Not sending loop {path} of {__instance.gameObject.GetFullHierarchyPath()}: it has no NitroxEntity");
        }
    }
}
