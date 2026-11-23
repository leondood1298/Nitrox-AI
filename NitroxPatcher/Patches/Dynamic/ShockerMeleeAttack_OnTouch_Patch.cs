using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     An Ampeel's shock does not go through MeleeAttack.CanBite. Its simulating player's game decides it like any bite,
///     on another player's body too (<see cref="CreatureAttacksOnPlayers.TryShockRemotePlayer" />); a copy on another game
///     only shows the shocks it is sent (<see cref="CreatureBites" />).
/// </summary>
public sealed partial class ShockerMeleeAttack_OnTouch_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((ShockerMeleeAttack t) => t.OnTouch(default(Collider)));

    public static bool Prefix(ShockerMeleeAttack __instance, Collider collider, out float __state)
    {
        __state = __instance.timeLastElectricalDamage;
        return Resolve<CreatureBites>().MayBite(__instance) && !Resolve<CreatureAttacksOnPlayers>().TryShockRemotePlayer(__instance, collider);
    }

    public static void Postfix(ShockerMeleeAttack __instance, bool __runOriginal, float __state)
    {
        // The shock landed on this game: the others show it
        if (__runOriginal && __instance.timeLastElectricalDamage != __state)
        {
            Resolve<CreatureBites>().Bit(__instance);
        }
    }
}
