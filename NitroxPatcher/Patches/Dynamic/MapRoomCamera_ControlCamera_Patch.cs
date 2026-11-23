using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The game shows the player a camera's view. That takes the camera's exclusive lock, which the patches on the
///     screen have asked for; with it, everybody else is told who controls the camera.
/// </summary>
public sealed partial class MapRoomCamera_ControlCamera_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomCamera t) => t.ControlCamera(null));

    public static bool Prefix(MapRoomCamera __instance, MapRoomScreen screen, out bool __state)
    {
        __state = Resolve<MapRoomCameras>().MayEnterView(__instance, screen);
        return __state;
    }

    public static void Postfix(MapRoomCamera __instance, bool __state)
    {
        if (__state)
        {
            Resolve<MapRoomCameras>().ControlStarted(__instance);
        }
    }
}
