using System;
using System.Reflection;
using Nitrox.Model.Helper;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
/// Ensures docked camera charging and its resulting component broadcast occur only on the Scanner Room's simulation owner.
/// </summary>
public sealed partial class MapRoomCamera_UpdateEnergyRecharge_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((MapRoomCamera t) => t.UpdateEnergyRecharge());

    public static bool Prefix(MapRoomCamera __instance)
    {
        // Only the room's simulator charges a docked camera, so the base power it draws is reported to whoever
        // simulates the power sources.
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
