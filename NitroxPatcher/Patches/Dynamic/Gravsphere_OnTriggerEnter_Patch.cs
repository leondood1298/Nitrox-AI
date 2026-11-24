using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Only the game that simulates a gravity trap pulls (see <see cref="GravityTraps" />).
/// </summary>
public sealed partial class Gravsphere_OnTriggerEnter_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Gravsphere t) => t.OnTriggerEnter(default(Collider)));

    public static bool Prefix(Gravsphere __instance, Collider collider)
    {
        return Resolve<GravityTraps>().MayAttract(__instance, collider);
    }
}
