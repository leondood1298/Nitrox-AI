using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A copy of another player's Reaper neither bites, nor starts the player death cinematic, nor grabs a vehicle on its own:
///     its simulating player's game decides those (<see cref="CreatureBites" />, <see cref="CreatureAttackEffects" />).
/// </summary>
public sealed partial class ReaperMeleeAttack_OnTouch_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((ReaperMeleeAttack t) => t.OnTouch(default(Collider)));

    public static bool Prefix(ReaperMeleeAttack __instance) => Resolve<CreatureBites>().MayBite(__instance);
}
