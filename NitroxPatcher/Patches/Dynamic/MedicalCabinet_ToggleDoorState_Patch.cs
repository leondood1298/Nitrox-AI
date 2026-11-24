using System.Reflection;
using NitroxClient.GameLogic.FMOD;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The door of a cabinet whose medkit another player took closes here two seconds later, through <c>Invoke</c>: the
///     player who took it already plays and sends that sound (see MedicalCabinetMetadataProcessor).
/// </summary>
public sealed partial class MedicalCabinet_ToggleDoorState_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MedicalCabinet t) => t.ToggleDoorState());

    public static void Prefix(MedicalCabinet __instance, out DelayedSilentReplay.Silence __state)
    {
        __state = DelayedSilentReplay.TakeMark(__instance);
    }

    public static void Finalizer(DelayedSilentReplay.Silence __state)
    {
        __state?.Dispose();
    }
}
