using System.Reflection;
using NitroxClient.GameLogic.FMOD;
using NitroxClient.MonoBehaviours;
using Nitrox.Model.GameLogic.FMOD;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class FMOD_CustomEmitter_OnStop_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((FMOD_CustomEmitter t) => t.OnStop());

    public static void Postfix(FMOD_CustomEmitter __instance)
    {
        if (!__instance.asset || !Resolve<FMODWhitelist>().IsWhitelisted(__instance.asset.path) || FMODSystem.IsOnRemotePlayer(__instance))
        {
            return;
        }

        if (__instance.TryGetComponentInParent(out NitroxEntity nitroxEntity, true))
        {
            string path = __instance.asset.path;
            if (FMODSystem.GetEventKind(path).Is3D)
            {
                Resolve<FMODSystem>().SendCustomEmitterStop(nitroxEntity.Id, path);
            }
            else
            {
                Resolve<FMODSystem>().SendEventInstanceStop(nitroxEntity.Id, path, __instance.transform.position.ToDto());
            }
        }
    }
}
