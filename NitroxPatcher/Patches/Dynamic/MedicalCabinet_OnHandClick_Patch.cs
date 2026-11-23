using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A click opens or closes the door or takes the medkit and starts the timer of the next one.
/// </summary>
public sealed partial class MedicalCabinet_OnHandClick_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MedicalCabinet t) => t.OnHandClick(default(GUIHand)));

    public static void Postfix(MedicalCabinet __instance)
    {
        Resolve<MedicalCabinets>().BroadcastState(__instance);
    }
}
