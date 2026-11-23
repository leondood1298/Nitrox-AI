using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The game removes a resource that was unloaded from the results of every room that scans for its type. Only the
///     room's simulating player lets it, and reports that the result is gone.
/// </summary>
public sealed partial class MapRoomFunctionality_OnResourceRemoved_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomFunctionality t) => t.OnResourceRemoved(null));

    public static bool Prefix(MapRoomFunctionality __instance) => Resolve<MapRoomScans>().ScansLocally(__instance);

    public static void Postfix(MapRoomFunctionality __instance, ResourceTrackerDatabase.ResourceInfo info) => Resolve<MapRoomScans>().ResourceRemoved(__instance, info);
}
