using System.Reflection;
using Nitrox.Model.DataStructures;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Register <see cref="WalkBehaviour"/> movements at <see cref="EntityPositionBroadcaster"/> for future broadcast.
/// </summary>
/// <remarks>
/// <see cref="WalkBehaviour.GoToInternal"/> overrides <see cref="SwimBehaviour.GoToInternal"/> and, while the creature is on a
/// surface, moves it through <see cref="OnSurfaceMovement"/> instead of <see cref="SplineFollowing"/>. A patch on the base
/// method is not run for it, so walking creatures (e.g. Cave Crawlers) need their own.
/// </remarks>
public sealed partial class WalkBehaviour_GoToInternal_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((WalkBehaviour t) => t.GoToInternal(default, default, default));

    public static void Prefix(WalkBehaviour __instance, Vector3 targetPosition, Vector3 targetDirection, float velocity)
    {
        if (__instance.TryGetIdOrWarn(out NitroxId entityId))
        {
            EntityPositionBroadcaster.Instance.RegisterSplineMovementChange(entityId, __instance.gameObject, targetPosition, targetDirection, velocity);
        }
    }
}
