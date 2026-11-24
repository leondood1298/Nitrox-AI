using System;
using System.Reflection;
using NitroxClient.GameLogic.FMOD;
using Nitrox.Model.GameLogic.FMOD;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     An animation event of a world object (a moonpool docking a vehicle, a door) plays in every game, because the
///     animation is synced: its sound is each game's own and not sent, and a non-positional one fades by distance. See
///     <see cref="WorldAnimationSounds" /> for another player's body and tool.
/// </summary>
public sealed partial class AnimationSound_OnAnimationSound_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((AnimationSound t) => t.OnAnimationSound(default(int)));

    public static void Prefix(AnimationSound __instance, int soundNum, out IDisposable __state)
    {
        FMODAsset asset = __instance.sounds != null && soundNum >= 0 && soundNum < __instance.sounds.Length ? __instance.sounds[soundNum] : null;
        __state = WorldAnimationSounds.Begin(__instance, asset, Resolve<FMODWhitelist>());
    }

    public static void Finalizer(IDisposable __state)
    {
        __state?.Dispose();
    }
}
