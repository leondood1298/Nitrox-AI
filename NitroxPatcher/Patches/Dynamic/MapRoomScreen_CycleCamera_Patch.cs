using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Cycling takes the player from the camera they control to the next one. They stay with the first until the
///     server grants the lock on the next; then the game changes over as it wrote it, which gives the first one up.
/// </summary>
public sealed partial class MapRoomScreen_CycleCamera_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomScreen t) => t.CycleCamera(default(int)));

    public static bool Prefix(MapRoomScreen __instance, int direction) =>
        Resolve<MapRoomCameras>().TryStartControl(__instance, direction, () => __instance.CycleCamera(direction));
}
