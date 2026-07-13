using System;
using System.Reflection;
using NitroxClient.GameLogic;
using Nitrox.Model.DataStructures;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class Battery_charge_set_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Property((Battery t) => t.charge).SetMethod;

    public static void Prefix(Battery __instance, float value)
    {
        // Scanner Room camera batteries are local implementation details. Their energy
        // is synchronized and persisted by MapRoomCameras using the camera entity id;
        // their temporary InstalledBatteryEntity ids do not exist on the server.
        if (__instance.GetComponentInParent<MapRoomCamera>())
        {
            return;
        }

        // Broadcast update only once per integer change
        if (Math.Abs(Math.Floor(__instance.charge) - Math.Floor(value)) > 0.0 &&
            __instance.TryGetIdOrWarn(out NitroxId id))
        {
            Entities entities = Resolve<Entities>();
            // Battery setters run while join-time entities are still being constructed.
            // Those objects may already have a Nitrox id locally but are not ready for
            // client-originated metadata updates yet.
            if (!entities.SpawningEntities && entities.IsKnownEntity(id))
            {
                entities.EntityMetadataChanged(__instance, id);
            }
        }
    }
}
