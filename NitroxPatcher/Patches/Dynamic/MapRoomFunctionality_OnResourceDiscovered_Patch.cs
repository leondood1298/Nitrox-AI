using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The game adds a resource that was loaded to the results of every room that scans for its type. Only the room's
///     simulating player lets it, and reports the result.
/// </summary>
public sealed partial class MapRoomFunctionality_OnResourceDiscovered_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomFunctionality t) => t.OnResourceDiscovered(null));

    public static bool Prefix(MapRoomFunctionality __instance) => Resolve<MapRoomScans>().ScansLocally(__instance);

    public static void Postfix(MapRoomFunctionality __instance, ResourceTrackerDatabase.ResourceInfo info) => Resolve<MapRoomScans>().ResourceDiscovered(__instance, info);
}
