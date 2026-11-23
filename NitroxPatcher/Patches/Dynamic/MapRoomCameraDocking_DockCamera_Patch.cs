using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class MapRoomCameraDocking_DockCamera_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomCameraDocking t) => t.DockCamera(null));

    public static void Postfix(MapRoomCameraDocking __instance, MapRoomCamera camera) => Resolve<MapRoomCameraDocks>().CameraDocked(__instance, camera);
}
