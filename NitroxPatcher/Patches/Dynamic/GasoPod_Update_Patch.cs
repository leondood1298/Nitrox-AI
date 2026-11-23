using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Only the game of the player simulating a Gasopod decides when it releases its pods (<see cref="CreatureEffects" />).
/// </summary>
public sealed partial class GasoPod_Update_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((GasoPod t) => t.Update());

    public static bool Prefix(GasoPod __instance) => Resolve<CreatureEffects>().MayReleaseGas(__instance);
}
