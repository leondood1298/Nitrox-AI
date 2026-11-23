using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class MapRoomCameraDocking_UndockCamera_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomCameraDocking t) => t.UndockCamera());

    public static void Prefix(MapRoomCameraDocking __instance, out MapRoomCamera __state)
    {
        __state = __instance.camera;
    }

    public static void Postfix(MapRoomCameraDocking __instance, MapRoomCamera __state) => Resolve<MapRoomCameraDocks>().CameraUndocked(__instance, __state);
}
