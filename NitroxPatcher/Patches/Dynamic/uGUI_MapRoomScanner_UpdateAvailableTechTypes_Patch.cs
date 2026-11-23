using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The game works out the types a room's screen lists from what this client's game has loaded. Only the room's
///     simulating player lets it, and reports the list; everybody else lists what the server sent.
/// </summary>
public sealed partial class uGUI_MapRoomScanner_UpdateAvailableTechTypes_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((uGUI_MapRoomScanner t) => t.UpdateAvailableTechTypes());

    public static bool Prefix(uGUI_MapRoomScanner __instance) => Resolve<MapRoomScans>().ObtainsTypesLocally(__instance);

    public static void Postfix(uGUI_MapRoomScanner __instance) => Resolve<MapRoomScans>().TypesObtained(__instance);
}
