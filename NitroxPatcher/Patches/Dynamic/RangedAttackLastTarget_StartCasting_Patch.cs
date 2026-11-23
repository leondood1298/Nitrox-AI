using System.Reflection;
using Nitrox.Model.Subnautica.Packets;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Broadcasts <see cref="RangedAttackLastTarget.StartCasting"/> from the simulating player.
/// </summary>
public sealed partial class RangedAttackLastTarget_StartCasting_Patch : NitroxPatch, IDynamicPatch
{
    internal static readonly MethodInfo TARGET_METHOD = Reflect.Method((RangedAttackLastTarget t) => t.StartCasting(default));

    public static void Prefix(RangedAttackLastTarget __instance)
    {
        RangedAttackLastTarget_StartCharging_Patch.BroadcastRangedAttack(__instance, RangedAttackLastTargetUpdate.ActionState.CASTING);
    }
}
