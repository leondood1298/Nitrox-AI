using System.Reflection;
using NitroxClient.GameLogic.FMOD;
using Nitrox.Model.GameLogic.FMOD;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     <see cref="FMOD_CustomEmitter.Play" /> starts the event before it calls <see cref="FMOD_CustomEmitter.OnPlay" />, so
///     silencing has to happen here. A non-positional sound repeated for another player's action is faded by the distance
///     to the emitter.
/// </summary>
public sealed partial class FMOD_CustomEmitter_Play_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((FMOD_CustomEmitter t) => t.Play());

    public static bool Prefix()
    {
        return !FMODSoundSuppressor.SuppressFMODEvents;
    }

    public static void Postfix(FMOD_CustomEmitter __instance, bool __runOriginal)
    {
        if (!__runOriginal || !ReplayedSoundScope.IsActive || !__instance.asset)
        {
            return;
        }
        if (FMODSystem.TryGetReplayedVolume(Resolve<FMODWhitelist>(), __instance.asset.path, __instance.transform.position, 1f, out float volume))
        {
            __instance.GetEventInstance().setVolume(volume);
        }
    }
}
