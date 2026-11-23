using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The game docks a camera that touches a free dock. Only the camera's simulating player lets it, like
///     <see cref="VehicleDockingBay_OnTriggerEnter" /> does for vehicles.
/// </summary>
public sealed partial class MapRoomCameraDocking_OnTriggerEnter_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomCameraDocking t) => t.OnTriggerEnter(null));

    public static bool Prefix(Collider other) => Resolve<MapRoomCameraDocks>().MayDockByItself(other);
}
