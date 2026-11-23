using System.Reflection;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class MapRoomCamera_ControlCamera_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomCamera t) => t.ControlCamera(null));

    public static bool Prefix(MapRoomCamera __instance, out bool __state)
    {
        __state = PacketSuppressor<MapRoomCameraControl>.IsSuppressed ||
            Resolve<MapRoomCameras>().CanBeginControl(__instance);
        return __state;
    }

    public static void Postfix(MapRoomCamera __instance, bool __state)
    {
        if (__state && !PacketSuppressor<MapRoomCameraControl>.IsSuppressed)
        {
            Resolve<MapRoomCameras>().BroadcastControl(__instance, isControlling: true);
        }
    }
}
