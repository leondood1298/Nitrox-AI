using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A room's object is destroyed when the room is deconstructed and when its base is built anew in a resync. What
///     is known about its scan ends with it: a room built in its place gets the same id.
/// </summary>
public sealed partial class MapRoomFunctionality_OnDestroy_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomFunctionality t) => t.OnDestroy());

    public static void Postfix(MapRoomFunctionality __instance) => Resolve<MapRoomScans>().RoomDestroyed(__instance);
}
