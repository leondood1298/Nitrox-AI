using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Reports the air in the player's air bladder as it is filled and used (see <see cref="OxygenTanks.AirBladderChanged" />).
/// </summary>
public sealed partial class AirBladder_UpdateInflateState_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((AirBladder t) => t.UpdateInflateState());

    public static void Prefix(AirBladder __instance, out float __state)
    {
        __state = __instance.oxygen;
    }

    public static void Postfix(AirBladder __instance, float __state)
    {
        Resolve<OxygenTanks>().AirBladderChanged(__instance, __state);
    }
}
