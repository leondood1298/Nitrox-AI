using System.Collections;
using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The game takes a picture of a camera's view for the rooms' screens before it takes the player out of that
///     view. Everybody else is sent the picture (see <see cref="MapRoomCameraPreviews" />).
/// </summary>
public sealed partial class MapRoomCamera_CaptureScreenAndExitLockedMode_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomCamera t) => t.CaptureScreenAndExitLockedMode());

    public static IEnumerator Postfix(IEnumerator __result, MapRoomCamera __instance) => Resolve<MapRoomCameraPreviews>().BroadcastAfterCapture(__result, __instance);
}
