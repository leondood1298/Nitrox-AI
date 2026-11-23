using System.Reflection;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class MapRoomFunctionality_StartScanning_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomFunctionality t) => t.StartScanning(TechType.None));

    public static void Postfix(MapRoomFunctionality __instance)
    {
        if (!PacketSuppressor<EntityMetadataUpdate>.IsSuppressed)
        {
            Resolve<MapRoomScans>().TargetChosen(__instance);
        }
    }
}
