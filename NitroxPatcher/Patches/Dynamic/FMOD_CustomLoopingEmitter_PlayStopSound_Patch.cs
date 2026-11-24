using System.Reflection;
using NitroxClient.GameLogic.FMOD;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The closing sound of a looping emitter is a sound like any other: silenced while sounds are. It is not sent: the
///     other games stop their copy of the emitter, which plays its own closing sound.
/// </summary>
public sealed partial class FMOD_CustomLoopingEmitter_PlayStopSound_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((FMOD_CustomLoopingEmitter t) => t.PlayStopSound());

    public static bool Prefix()
    {
        return !FMODSoundSuppressor.SuppressFMODEvents;
    }
}
