using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class MapRoomCamera_HandleInput_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomCamera t) => t.HandleInput());

    public static void Postfix(MapRoomCamera __instance)
    {
        Resolve<MapRoomCameraStates>().ReportIfChanged(__instance);
    }
}
