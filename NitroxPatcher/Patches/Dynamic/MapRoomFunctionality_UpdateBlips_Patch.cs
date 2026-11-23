using System.Reflection;
using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class MapRoomFunctionality_UpdateBlips_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomFunctionality t) => t.UpdateBlips());

    public static void Prefix(MapRoomFunctionality __instance, out int __state)
    {
        __state = __instance.numNodesScanned;
    }

    public static void Postfix(MapRoomFunctionality __instance, int __state)
    {
        if (__instance.numNodesScanned != __state && __instance.TryGetNitroxId(out NitroxId roomId) && Resolve<SimulationOwnership>().HasAnyLockType(roomId))
        {
            Resolve<Entities>().EntityMetadataChangedThrottled(__instance, roomId);
        }
    }
}
