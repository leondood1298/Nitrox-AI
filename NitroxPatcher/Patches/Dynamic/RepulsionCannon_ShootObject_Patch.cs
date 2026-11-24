using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Hands a repulsion cannon's push on an object another player simulates to that player's game (see <see cref="Repulsions" />).
/// </summary>
public sealed partial class RepulsionCannon_ShootObject_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((RepulsionCannon t) => t.ShootObject(default(Rigidbody), default(Vector3)));

    public static void Postfix(Rigidbody rb, Vector3 velocity)
    {
        Resolve<Repulsions>().Shot(rb, velocity);
    }
}
