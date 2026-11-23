using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The game reveals one more of the room's results every scan interval. The room's simulating player reports how
///     far it has got.
/// </summary>
public sealed partial class MapRoomFunctionality_UpdateBlips_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomFunctionality t) => t.UpdateBlips());

    public static void Prefix(MapRoomFunctionality __instance, out int __state) => __state = __instance.numNodesScanned;

    public static void Postfix(MapRoomFunctionality __instance, int __state) => Resolve<MapRoomScans>().BlipsUpdated(__instance, __state);
}
