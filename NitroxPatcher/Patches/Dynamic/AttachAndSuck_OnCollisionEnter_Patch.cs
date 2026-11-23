using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A copy of another player's Bleeder does not latch onto this game's player on its own (<see cref="Bleeders" />).
/// </summary>
public sealed partial class AttachAndSuck_OnCollisionEnter_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((AttachAndSuck t) => t.OnCollisionEnter(default(Collision)));

    public static bool Prefix(AttachAndSuck __instance) => Resolve<Bleeders>().MayLatchOnHere(__instance);
}
