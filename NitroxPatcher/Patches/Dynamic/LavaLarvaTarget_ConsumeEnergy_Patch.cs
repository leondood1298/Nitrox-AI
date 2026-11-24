using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A Lava Larva drains the copy of the vehicle in the game simulating the larva. The drain is passed to the game that
///     simulates the vehicle instead (see <see cref="LavaLarvae" />).
/// </summary>
public sealed partial class LavaLarvaTarget_ConsumeEnergy_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((LavaLarvaTarget t) => t.ConsumeEnergy(default));

    public static bool Prefix(LavaLarvaTarget __instance, float amount, ref float __result)
    {
        if (Resolve<LavaLarvae>().DrainsHere(__instance, amount))
        {
            return true;
        }
        __result = amount;
        return false;
    }
}
