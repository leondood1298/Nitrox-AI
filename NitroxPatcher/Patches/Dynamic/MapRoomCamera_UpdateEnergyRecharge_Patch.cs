using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A docked camera is recharged, and its battery reported, by the player who simulates the camera.
/// </summary>
public sealed partial class MapRoomCamera_UpdateEnergyRecharge_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomCamera t) => t.UpdateEnergyRecharge());

    public static bool Prefix(MapRoomCamera __instance)
    {
        // The base power the camera draws is reported to whoever simulates the power sources.
        BasePowerConsumptionForwarder forwarder = Resolve<BasePowerConsumptionForwarder>();
        forwarder.EnterConsumer();
        try
        {
            Resolve<MapRoomCameras>().UpdateEnergyRecharge(__instance);
        }
        finally
        {
            forwarder.ExitConsumer();
        }
        return false;
    }
}
