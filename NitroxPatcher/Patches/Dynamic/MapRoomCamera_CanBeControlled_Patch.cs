using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The game decides here whether a room's screen offers a camera to the player. A camera another player controls
///     is taken out of that offer (see <see cref="MapRoomCameras" />).
/// </summary>
public sealed partial class MapRoomCamera_CanBeControlled_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomCamera t) => t.CanBeControlled(null));

    public static void Postfix(MapRoomCamera __instance, ref bool __result) => __result = Resolve<MapRoomCameras>().CanBeControlled(__instance, __result);
}
