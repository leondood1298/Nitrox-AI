using System;
using System.Reflection;
using NitroxClient.GameLogic.FMOD;
using Nitrox.Model.GameLogic.FMOD;

namespace NitroxPatcher.Patches.Dynamic;

/// <inheritdoc cref="AnimationSound_OnAnimationSound_Patch" />
public sealed partial class GenericPlaySound_PlaySound_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((GenericPlaySound t) => t.PlaySound());

    public static void Prefix(GenericPlaySound __instance, out IDisposable __state)
    {
        __state = WorldAnimationSounds.Begin(__instance, __instance.sound, Resolve<FMODWhitelist>());
    }

    public static void Finalizer(IDisposable __state)
    {
        __state?.Dispose();
    }
}
