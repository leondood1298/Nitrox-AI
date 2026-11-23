using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The game updates a room's scan every frame, in every player's game, and drains the room's power from here.
///     <see cref="MapRoomScans" /> does its own work for the room at that pace, and keeps the drain to the player
///     who simulates the room.
/// </summary>
public sealed partial class MapRoomFunctionality_UpdateScanning_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomFunctionality t) => t.UpdateScanning());

    public static void Prefix(MapRoomFunctionality __instance, out bool __state) => __state = Resolve<MapRoomScans>().BeforeScanUpdate(__instance);

    public static void Finalizer(bool __state) => Resolve<MapRoomScans>().ScanUpdated(__state);
}
