using System;
using System.Reflection;
using Nitrox.Model.Helper;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class MapRoomCameraDocking_DockCamera_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomCameraDocking t) => t.DockCamera(null));

    public static void Postfix(MapRoomCameraDocking __instance, MapRoomCamera camera)
    {
        if (MapRoomCameras.EnsureCameraId(__instance, camera))
        {
            Resolve<MapRoomCameras>().BroadcastDock(__instance, camera);
        }
    }
}
