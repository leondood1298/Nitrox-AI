using System.Reflection;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.FMOD;
using NitroxClient.MonoBehaviours.Cyclops;
using Nitrox.Model.DataStructures;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Broadcasts openables (door, traps) being opened/closed. Replicates this state for the associated virtual cyclops.
/// </summary>
public sealed partial class Openable_PlayOpenAnimation_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Openable t) => t.PlayOpenAnimation(default(bool), default(float)));

    public static bool Prefix(Openable __instance, bool openState, float duration, out ReplayedSoundScope? __state)
    {
        __state = null;
        if (__instance.TryGetComponentInParent(out NitroxCyclops nitroxCyclops) && nitroxCyclops.Virtual)
        {
            nitroxCyclops.Virtual.ReplicateOpening(__instance, openState, duration);
        }

        // Do not try to sync
        if (__instance.GetComponentInParent<VirtualCyclops>())
        {
            return true;
        }

        if (__instance.isOpen != openState && __instance.TryGetIdOrWarn(out NitroxId id))
        {
            Resolve<Interior>().OpenableStateChanged(id, openState, duration);
            // Every game opens the door when it gets this and plays its sound there, so the sound is not sent as well
            __state = ReplayedSoundScope.Enter();
        }

        return true;
    }

    public static void Finalizer(ReplayedSoundScope? __state)
    {
        __state?.Dispose();
    }
}
