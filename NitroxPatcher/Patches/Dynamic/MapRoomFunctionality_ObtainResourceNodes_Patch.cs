using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The game fills a room's results with the matching resources this client's game has loaded in range. It does so
///     when a scan starts, when the room starts existing with a scan going on, and when the scan range changes. Only
///     the room's simulating player lets it, and then reports the list; everybody else keeps what the server sent.
/// </summary>
public sealed partial class MapRoomFunctionality_ObtainResourceNodes_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomFunctionality t) => t.ObtainResourceNodes(default));

    public static bool Prefix(MapRoomFunctionality __instance, TechType typeToScan) => Resolve<MapRoomScans>().ObtainsResultsLocally(__instance, typeToScan);

    public static void Postfix(MapRoomFunctionality __instance) => Resolve<MapRoomScans>().ResultsObtained(__instance);
}
