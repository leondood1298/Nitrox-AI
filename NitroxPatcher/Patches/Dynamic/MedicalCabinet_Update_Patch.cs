using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     MedicalCabinet.Update makes the next medkit once its time has come. The server stores that the cabinet holds one,
///     so that a player who joins later finds it there.
/// </summary>
public sealed partial class MedicalCabinet_Update_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MedicalCabinet t) => t.Update());

    public static void Prefix(MedicalCabinet __instance, out bool __state)
    {
        __state = __instance.hasMedKit;
    }

    public static void Postfix(MedicalCabinet __instance, bool __state)
    {
        if (!__state && __instance.hasMedKit)
        {
            Resolve<MedicalCabinets>().BroadcastState(__instance);
        }
    }
}
