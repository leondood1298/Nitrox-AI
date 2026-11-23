using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class MapRoomCamera_ExitLockedMode_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomCamera t) => t.ExitLockedMode(resetPlayerPosition: false));

    public static void Postfix(MapRoomCamera __instance) => Resolve<MapRoomCameras>().ControlEnded(__instance);
}
