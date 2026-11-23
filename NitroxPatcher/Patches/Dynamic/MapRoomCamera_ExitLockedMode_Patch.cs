using System.Reflection;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class MapRoomCamera_ExitLockedMode_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomCamera t) => t.ExitLockedMode(resetPlayerPosition: false));

    public static void Postfix(MapRoomCamera __instance)
    {
        if (!PacketSuppressor<MapRoomCameraControl>.IsSuppressed)
        {
            Resolve<MapRoomCameras>().BroadcastControl(__instance, isControlling: false);
        }
    }
}
