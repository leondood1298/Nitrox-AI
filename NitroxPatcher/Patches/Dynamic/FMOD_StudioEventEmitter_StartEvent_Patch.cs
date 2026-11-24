using System.Reflection;
using NitroxClient.GameLogic.FMOD;
using Nitrox.Model.GameLogic.FMOD;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The other way a studio emitter starts (stasis rifle charge, flare throw, Seamoth perimeter defense charge,
///     breathing). Sent like <see cref="FMOD_StudioEventEmitter.PlayUI" />.
/// </summary>
public sealed partial class FMOD_StudioEventEmitter_StartEvent_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((FMOD_StudioEventEmitter t) => t.StartEvent());

    public static bool Prefix()
    {
        return !FMODSoundSuppressor.SuppressFMODEvents;
    }

    public static void Postfix(FMOD_StudioEventEmitter __instance, bool __runOriginal)
    {
        if (__runOriginal && __instance.evt.isValid())
        {
            Resolve<FMODSystem>().SendStudioEmitterStarted(__instance, Resolve<FMODWhitelist>());
        }
    }
}
