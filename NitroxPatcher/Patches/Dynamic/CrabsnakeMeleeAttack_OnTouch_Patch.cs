using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A copy of another player's Crabsnake does not grab or bite on its own: its simulating player's game decides the
///     bite and the bitten player's game plays the grab (<see cref="CreatureAttackEffects" />).
/// </summary>
public sealed partial class CrabsnakeMeleeAttack_OnTouch_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((CrabsnakeMeleeAttack t) => t.OnTouch(default(Collider)));

    public static bool Prefix(CrabsnakeMeleeAttack __instance) => Resolve<CreatureBites>().MayBite(__instance);
}
