using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A Crabsquid's EMP blast is cast on every game, so it reaches every player's vehicle and tools (<see cref="CreatureEffects" />).
/// </summary>
public sealed partial class EMPAttack_CastEMPBlast_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((EMPAttack t) => t.CastEMPBlast());

    public static void Postfix(EMPAttack __instance) => Resolve<CreatureEffects>().EmpBlastCast(__instance);
}
