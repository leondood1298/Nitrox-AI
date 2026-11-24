using System.Reflection;
using NitroxClient.GameLogic.FMOD;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     An animation event of a world object (a moonpool docking a vehicle, a door) plays in every game, because the
///     animation is synced: its sound is each game's own and not sent, and a non-positional one fades by distance.
/// </summary>
public sealed partial class AnimationSound_OnAnimationSound_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((AnimationSound t) => t.OnAnimationSound(default(int)));

    public static void Prefix(AnimationSound __instance, out ReplayedSoundScope? __state)
    {
        __state = WorldAnimationSounds.Begin(__instance);
    }

    public static void Finalizer(ReplayedSoundScope? __state)
    {
        __state?.Dispose();
    }
}
