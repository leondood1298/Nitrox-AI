using System;
using Nitrox.Model.DataStructures;
using NitroxClient.MonoBehaviours;

namespace NitroxClient.GameLogic;

/// <summary>
///     Reports a battery's charge to the server, which keeps it for the other players, a joining one and a restart.
/// </summary>
public class BatteryCharges
{
    private readonly Entities entities;

    public BatteryCharges(Entities entities)
    {
        this.entities = entities;
    }

    /// <summary>
    ///     Called after a battery's charge was set.
    /// </summary>
    public void Changed(Battery battery, float previousCharge)
    {
        // Scanner Room camera batteries are local implementation details. Their energy is synchronized and persisted by
        // MapRoomCameraStates using the camera entity id; their temporary InstalledBatteryEntity ids do not exist on the server.
        if (!ShouldReport(previousCharge, battery.charge, battery.capacity) || battery.GetComponentInParent<MapRoomCamera>() || !battery.TryGetIdOrWarn(out NitroxId id))
        {
            return;
        }
        // Battery setters run while join-time entities are still being constructed. Those objects may already have a
        // Nitrox id locally but are not ready for client-originated metadata updates yet.
        if (!entities.SpawningEntities && entities.IsKnownEntity(id))
        {
            entities.EntityMetadataChanged(battery, id);
        }
    }

    /// <summary>
    ///     A charge is reported once per whole unit it crosses, and when it becomes empty or full: the stored charge is
    ///     then never more than a unit away, and an empty or full battery is stored as such.
    /// </summary>
    public static bool ShouldReport(float previous, float current, float capacity)
    {
        if (previous == current)
        {
            return false;
        }
        return Math.Floor(previous) != Math.Floor(current) || current <= 0f || current >= capacity;
    }
}
