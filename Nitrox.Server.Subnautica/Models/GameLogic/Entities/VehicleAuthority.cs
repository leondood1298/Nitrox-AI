using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

internal sealed class VehicleAuthority(EntityRegistry entityRegistry, SimulationOwnershipData simulationOwnershipData)
{
    internal const int MAX_MOVEMENTS_PER_PACKET = 64;

    private readonly EntityRegistry entityRegistry = entityRegistry;
    private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;
    private readonly Dictionary<NitroxId, (NitroxId DockId, Player Player)> pendingUndocks = [];

    public bool TryGetOwnedWorldEntity(Player sender, NitroxId id, bool requireExclusive, out WorldEntity worldEntity, out string rejectionReason)
    {
        if (!entityRegistry.TryGetEntityById(id, out worldEntity))
        {
            rejectionReason = "entity is unknown or cannot move";
            return false;
        }

        if (!sender.CanSee(worldEntity))
        {
            rejectionReason = "entity is not visible to the sender";
            return false;
        }

        if (!TryValidateLock(sender, id, requireExclusive, out rejectionReason))
        {
            return false;
        }

        rejectionReason = string.Empty;
        return true;
    }

    public bool TryGetOwnedVehicle(Player sender, NitroxId id, bool requireExclusive, out VehicleEntity vehicle, out string rejectionReason)
    {
        if (!entityRegistry.TryGetEntityById(id, out vehicle))
        {
            rejectionReason = "entity is not a vehicle";
            return false;
        }

        if (!sender.CanSee(vehicle))
        {
            rejectionReason = "vehicle is not visible to the sender";
            return false;
        }

        return TryValidateLock(sender, id, requireExclusive, out rejectionReason);
    }

    public bool TryValidateDocking(Player sender, NitroxId vehicleId, NitroxId dockId, out VehicleEntity vehicle, out Entity dock, out string rejectionReason)
    {
        dock = null!;
        if (!TryGetOwnedVehicle(sender, vehicleId, true, out vehicle, out rejectionReason))
        {
            return false;
        }

        if (!IsDockableVehicle(vehicle))
        {
            rejectionReason = "vehicle type cannot dock";
            return false;
        }

        if (!entityRegistry.TryGetEntityById(dockId, out dock) || !IsDock(dock))
        {
            rejectionReason = "entity is not a compatible vehicle dock";
            return false;
        }

        if (!sender.CanSee(dock))
        {
            rejectionReason = "dock is not visible to the sender";
            return false;
        }

        bool dockAvailable = !dock.ChildEntities.OfType<VehicleEntity>().Any(entity => entity.Id != vehicleId);
        if (!CanDock(vehicle.ParentId == null, dockAvailable))
        {
            rejectionReason = vehicle.ParentId == dockId ? "vehicle is already docked here" :
                              vehicle.ParentId != null ? "vehicle is already parented" : "dock is occupied";
            return false;
        }

        rejectionReason = string.Empty;
        lock (pendingUndocks)
        {
            pendingUndocks.Remove(vehicleId);
        }
        return true;
    }

    public void MarkUndockingStarted(Player sender, NitroxId vehicleId, NitroxId dockId)
    {
        lock (pendingUndocks)
        {
            pendingUndocks[vehicleId] = (dockId, sender);
        }
    }

    public bool TryValidateUndockingCompletion(Player sender, NitroxId vehicleId, NitroxId dockId, out string rejectionReason)
    {
        if (!TryGetOwnedVehicle(sender, vehicleId, true, out VehicleEntity vehicle, out rejectionReason))
        {
            return false;
        }

        lock (pendingUndocks)
        {
            if (!pendingUndocks.TryGetValue(vehicleId, out (NitroxId DockId, Player Player) pending) ||
                pending.DockId != dockId || pending.Player != sender)
            {
                rejectionReason = "undocking finish has no matching start";
                return false;
            }

            if (vehicle.ParentId != null)
            {
                rejectionReason = "vehicle is still parented while finishing undocking";
                return false;
            }

            pendingUndocks.Remove(vehicleId);
        }

        rejectionReason = string.Empty;
        return true;
    }

    public bool TryValidateUndocking(Player sender, NitroxId vehicleId, NitroxId dockId, out VehicleEntity vehicle, out Entity dock, out string rejectionReason)
    {
        dock = null!;
        if (!TryGetOwnedVehicle(sender, vehicleId, true, out vehicle, out rejectionReason))
        {
            return false;
        }

        if (!entityRegistry.TryGetEntityById(dockId, out dock) || !IsDock(dock))
        {
            rejectionReason = "entity is not a compatible vehicle dock";
            return false;
        }

        if (vehicle.ParentId != dockId)
        {
            rejectionReason = "vehicle is not docked in the requested dock";
            return false;
        }

        rejectionReason = string.Empty;
        return true;
    }

    public bool TryValidatePilotChange(Player sender, NitroxId vehicleId, bool isPiloting, out VehicleEntity vehicle, out string rejectionReason)
    {
        if (!TryGetOwnedVehicle(sender, vehicleId, isPiloting, out vehicle, out rejectionReason))
        {
            return false;
        }

        NitroxId? currentVehicle = sender.PlayerContext?.DrivingVehicle;
        bool validTransition = isPiloting ? currentVehicle == null || currentVehicle == vehicleId : currentVehicle == vehicleId;
        if (!validTransition)
        {
            rejectionReason = isPiloting ? "sender is already piloting another vehicle" : "sender is not piloting this vehicle";
            return false;
        }

        rejectionReason = string.Empty;
        return true;
    }

    public bool IsCyclops(NitroxId id) => entityRegistry.TryGetEntityById(id, out VehicleEntity vehicle) && IsCyclops(vehicle);

    internal static bool CanDock(bool vehicleIsUnparented, bool dockAvailable) => vehicleIsUnparented && dockAvailable;

    internal static bool IsFinite(MovementData movementData)
    {
        if (!IsFiniteTransform(movementData.Position, movementData.Rotation))
        {
            return false;
        }

        return movementData is not ExosuitMovementData exosuit ||
               IsFinite(exosuit.AimTargetLeft) && IsFinite(exosuit.AimTargetRight);
    }

    internal static bool IsValidMovementCount(int count) => count is > 0 and <= MAX_MOVEMENTS_PER_PACKET;

    internal static bool IsFiniteTransform(Nitrox.Model.DataStructures.Unity.NitroxVector3 position,
                                           Nitrox.Model.DataStructures.Unity.NitroxQuaternion rotation) =>
        IsFinite(position) && IsFinite(rotation);

    private bool TryValidateLock(Player sender, NitroxId id, bool requireExclusive, out string rejectionReason)
    {
        if (!simulationOwnershipData.TryGetLock(id, out SimulationOwnershipData.PlayerLock playerLock))
        {
            rejectionReason = "entity has no simulation owner";
            return false;
        }

        if (playerLock.Player != sender)
        {
            rejectionReason = "sender does not own the simulation lock";
            return false;
        }

        if (requireExclusive && playerLock.LockType != SimulationLockType.EXCLUSIVE)
        {
            rejectionReason = "operation requires an exclusive simulation lock";
            return false;
        }

        rejectionReason = string.Empty;
        return true;
    }

    private bool IsDock(Entity entity)
    {
        if (entity is MoonpoolEntity)
        {
            return true;
        }

        return entity is PathBasedChildEntity && entity.ParentId != null &&
               entityRegistry.TryGetEntityById(entity.ParentId, out VehicleEntity parentVehicle) && IsCyclops(parentVehicle);
    }

    private static bool IsCyclops(VehicleEntity vehicle) => vehicle.TechType?.Name == "Cyclops";

    private static bool IsDockableVehicle(VehicleEntity vehicle) => vehicle.TechType?.Name is "Seamoth" or "SeaMoth" or "Exosuit";

    private static bool IsFinite(Nitrox.Model.DataStructures.Unity.NitroxVector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static bool IsFinite(Nitrox.Model.DataStructures.Unity.NitroxQuaternion value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z) && float.IsFinite(value.W) &&
        value.X * value.X + value.Y * value.Y + value.Z * value.Z + value.W * value.W > 0.0001f;
}
