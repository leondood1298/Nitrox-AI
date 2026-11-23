using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A Stalker that drops a Scanner Room camera gives the camera's lock back, see <see cref="StalkerCameraGrabs" />.
/// </summary>
public sealed partial class CollectShiny_DropShinyTarget_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((CollectShiny t) => t.DropShinyTarget(default(GameObject)));

    public static void Prefix(GameObject target) => Resolve<StalkerCameraGrabs>().Dropped(target);
}
