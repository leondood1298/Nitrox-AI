using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     For a resource it already has, the game only takes over the position the resource is registered with this time,
///     and tells nobody. A resource that moved that way is reported for the rooms that scan for it (see
///     <see cref="MapRoomScans" />).
/// </summary>
public sealed partial class ResourceTrackerDatabase_Register_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method(() => ResourceTrackerDatabase.Register(string.Empty, Vector3.zero, TechType.None));

    public static void Prefix(string uniqueId, TechType resourceTechType, out Vector3? __state) => __state = MapRoomScans.FindRegisteredPosition(uniqueId, resourceTechType);

    public static void Postfix(string uniqueId, Vector3 resourcePosition, TechType resourceTechType, Vector3? __state) =>
        Resolve<MapRoomScans>().ResourceRegistered(uniqueId, resourceTechType, resourcePosition, __state);
}
