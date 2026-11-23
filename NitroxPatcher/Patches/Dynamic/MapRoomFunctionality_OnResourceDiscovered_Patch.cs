using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class MapRoomFunctionality_OnResourceDiscovered_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomFunctionality t) => t.OnResourceDiscovered(null));

    public static bool Prefix(MapRoomFunctionality __instance) =>
        Resolve<MapRoomScanResultBroadcaster>().ShouldRunVanillaResults(__instance);

    public static void Postfix(MapRoomFunctionality __instance, ResourceTrackerDatabase.ResourceInfo info)
    {
        Resolve<MapRoomScanResultBroadcaster>().BroadcastDiscovered(__instance, info);
    }
}
