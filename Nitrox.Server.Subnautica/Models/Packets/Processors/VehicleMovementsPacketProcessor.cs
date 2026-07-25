using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class VehicleMovementsPacketProcessor(EntityRegistry entityRegistry,
    SimulationOwnershipData simulationOwnershipData, MapRoomCameraControlLifecycle cameraControlLifecycle,
    ILogger<VehicleMovementsPacketProcessor> logger)
    : IAuthPacketProcessor<VehicleMovements>
{
    internal const int MAX_MOVEMENTS_PER_PACKET = 64;

    private static readonly NitroxVector3 CyclopsSteeringWheelRelativePosition = new(-0.05f, 0.97f, -23.54f);

    private readonly EntityRegistry entityRegistry = entityRegistry;
    private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;
    private readonly MapRoomCameraControlLifecycle cameraControlLifecycle = cameraControlLifecycle;
    private readonly ILogger<VehicleMovementsPacketProcessor> logger = logger;

    public async Task Process(AuthProcessorContext context, VehicleMovements packet)
    {
        if (!IsValidMovementCount(packet.Data?.Count ?? 0) || !IsValidRealTime(packet.RealTime))
        {
            logger.ZLogWarningOnce($"Rejected malformed movement batch from {context.Sender.Name} #{context.Sender.SessionId}");
            return;
        }

        // Scanner Room topology is relevant only to known camera entries. Capture it at most once,
        // on first use, so every camera in this packet observes one consistent snapshot without
        // making ordinary vehicle/creature movement enumerate and lock every Scanner Room.
        Lazy<Dictionary<NitroxId, CameraRegistrationState>> registrationStates =
            new(() => GetCameraRegistrationStates(packet.Data));
        List<(MovementData Movement, WorldEntity? Entity, bool RequiresCanonicalControl)> candidates =
            new(packet.Data.Count);
        foreach (MovementData movementData in packet.Data)
        {
            if (!IsFiniteMovement(movementData))
            {
                logger.ZLogWarningOnce($"Rejected non-finite movement entry from {context.Sender.Name} #{context.Sender.SessionId}");
                continue;
            }

            if (entityRegistry.TryGetEntityById(movementData.Id, out WorldEntity worldEntity) && worldEntity.Transform != null)
            {
                bool validWorldCamera = string.Equals(
                    worldEntity.TechType?.Name, "MapRoomCamera", StringComparison.Ordinal);
                CameraRegistrationState registrationState = default;
                if (validWorldCamera || cameraControlLifecycle.IsKnown(movementData.Id))
                {
                    registrationStates.Value.TryGetValue(movementData.Id,
                        out registrationState);
                }
                if (registrationState.HasAssociation && !validWorldCamera)
                {
                    logger.ZLogWarningOnce(
                        $"Rejected Scanner Room camera ID collision for {movementData.Id}");
                    continue;
                }

                bool requiresCanonicalControl = false;
                if (validWorldCamera)
                {
                    if (registrationState.IsAmbiguous)
                    {
                        logger.ZLogWarningOnce(
                            $"Rejected ambiguously registered Scanner Room camera movement for {movementData.Id}");
                        continue;
                    }
                    requiresCanonicalControl = registrationState.IsDocked;
                }
                candidates.Add((movementData, worldEntity, requiresCanonicalControl));
                continue;
            }

            // A newly built Scanner Room can register its canonical cameras before those cameras
            // exist as server-side world entities. Relay finite simple movement for that bounded
            // bootstrap gap, but never synthesize an entity or use the exception for an existing
            // non-world/transform-less entity, a different movement shape, or an ambiguous ID.
            // The lock lookup is only a cheap prefilter before the room scan; ExecuteForOwner
            // rechecks the owner and lock type below while ownership is synchronized.
            if (!entityRegistry.GetEntityById(movementData.Id).HasValue &&
                movementData is SimpleMovementData &&
                simulationOwnershipData.TryGetLock(movementData.Id, out SimulationOwnershipData.PlayerLock candidateLock) &&
                candidateLock.Player == context.Sender &&
                candidateLock.LockType == SimulationLockType.EXCLUSIVE &&
                cameraControlLifecycle.IsActiveController(movementData.Id, context.Sender.SessionId) &&
                registrationStates.Value.TryGetValue(movementData.Id,
                    out CameraRegistrationState fallbackRegistrationState) &&
                fallbackRegistrationState.IsExactlyOneRegistration)
            {
                candidates.Add((movementData, null, true));
                continue;
            }

            logger.ZLogWarningOnce($"Player {context.Sender.Name} tried updating unknown or non-world entity {movementData.Id}");
        }

        int unauthorizedCount = 0;
        int nonExclusiveScannerCount = 0;
        int nonPilotCount = 0;
        Task sendTask = simulationOwnershipData.ExecuteForOwner(context.Sender,
            candidates.Select(candidate => candidate.Movement.Id), ownedIds =>
            {
                List<MovementData> acceptedMovements = new(candidates.Count);
                foreach ((MovementData movementData, WorldEntity? worldEntity,
                             bool requiresCanonicalControl) in candidates)
                {
                    if (!ownedIds.Contains(movementData.Id))
                    {
                        unauthorizedCount++;
                        continue;
                    }
                    if (requiresCanonicalControl &&
                        (!simulationOwnershipData.TryGetLock(movementData.Id, out SimulationOwnershipData.PlayerLock playerLock) ||
                         playerLock.Player != context.Sender ||
                         playerLock.LockType != SimulationLockType.EXCLUSIVE ||
                         !cameraControlLifecycle.IsActiveController(
                             movementData.Id, context.Sender.SessionId)))
                    {
                        nonExclusiveScannerCount++;
                        continue;
                    }
                    if (movementData is DrivenVehicleMovementData && context.Sender.PlayerContext?.DrivingVehicle != movementData.Id)
                    {
                        nonPilotCount++;
                        continue;
                    }

                    if (worldEntity != null)
                    {
                        worldEntity.Transform.Position = movementData.Position;
                        worldEntity.Transform.Rotation = movementData.Rotation;
                    }

                    if (movementData is DrivenVehicleMovementData)
                    {
                        // Cyclops' driving wheel is at a known position so we need to adapt the position of the player accordingly
                        if (string.Equals(worldEntity?.TechType?.Name, "Cyclops", StringComparison.Ordinal))
                        {
                            context.Sender.Entity.Transform.LocalPosition = CyclopsSteeringWheelRelativePosition;
                            context.Sender.Position = context.Sender.Entity.Transform.Position;
                        }
                        else
                        {
                            context.Sender.Position = movementData.Position;
                            context.Sender.Rotation = movementData.Rotation;
                        }
                    }
                    acceptedMovements.Add(movementData);
                }

                // LiteNetLib enqueues synchronously. Invoke the send while ownership is locked so a
                // later release/reassignment packet cannot overtake this accepted movement batch.
                return acceptedMovements.Count > 0
                    ? context.SendToOthersAsync(new VehicleMovements(acceptedMovements, packet.RealTime))
                    : Task.CompletedTask;
            });
        await sendTask;
        if (unauthorizedCount > 0)
        {
            logger.ZLogWarningOnce($"Player {context.Sender.Name} sent {unauthorizedCount} movement entr{(unauthorizedCount == 1 ? "y" : "ies")} without owning the simulation lock");
        }
        if (nonExclusiveScannerCount > 0)
        {
            logger.ZLogWarningOnce($"Rejected {nonExclusiveScannerCount} Scanner Room camera movement entr{(nonExclusiveScannerCount == 1 ? "y" : "ies")} from {context.Sender.Name} without exclusive control");
        }
        if (nonPilotCount > 0)
        {
            logger.ZLogWarningOnce($"Rejected {nonPilotCount} driven movement entr{(nonPilotCount == 1 ? "y" : "ies")} from {context.Sender.Name}: sender is not the canonical pilot");
        }
    }

    private Dictionary<NitroxId, CameraRegistrationState> GetCameraRegistrationStates(
        IEnumerable<MovementData> movements)
    {
        HashSet<NitroxId> requestedIds = [];
        foreach (MovementData? movement in movements)
        {
            if (movement?.Id != null)
            {
                requestedIds.Add(movement.Id);
            }
        }

        Dictionary<NitroxId, CameraRegistrationState> states =
            requestedIds.ToDictionary(cameraId => cameraId,
                _ => default(CameraRegistrationState));
        foreach (MapRoomEntity room in entityRegistry.GetEntities<MapRoomEntity>())
        {
            lock (room)
            {
                foreach (MapRoomCameraRecord? record in room.CameraRegistry)
                {
                    if (record?.CameraId != null &&
                        states.TryGetValue(record.CameraId,
                            out CameraRegistrationState state))
                    {
                        states[record.CameraId] = new CameraRegistrationState(
                            state.Registrations + 1, state.DockedAssociations);
                    }
                }
                IncrementDockedAssociation(states, room.LeftDockCameraId);
                IncrementDockedAssociation(states, room.RightDockCameraId);
            }
        }
        return states;
    }

    private static void IncrementDockedAssociation(
        Dictionary<NitroxId, CameraRegistrationState> states, NitroxId? cameraId)
    {
        if (cameraId != null &&
            states.TryGetValue(cameraId, out CameraRegistrationState state))
        {
            states[cameraId] = new CameraRegistrationState(
                state.Registrations, state.DockedAssociations + 1);
        }
    }

    private readonly record struct CameraRegistrationState(
        int Registrations,
        int DockedAssociations)
    {
        internal bool HasAssociation =>
            Registrations > 0 || DockedAssociations > 0;

        internal bool IsAmbiguous =>
            Registrations > 1 || DockedAssociations > 1 ||
            DockedAssociations == 1 && Registrations != 1;

        internal bool IsDocked => !IsAmbiguous && DockedAssociations == 1;

        internal bool IsExactlyOneRegistration =>
            !IsAmbiguous && Registrations == 1;
    }

    internal static bool IsValidMovementCount(int count) => count is > 0 and <= MAX_MOVEMENTS_PER_PACKET;

    internal static bool IsValidRealTime(double realTime) =>
        double.IsFinite(realTime) && realTime >= 0d && realTime <= float.MaxValue;

    internal static bool IsFiniteMovement(MovementData? movementData)
    {
        if (movementData is null || movementData.Id is null || !IsFinite(movementData.Position) || !IsFinite(movementData.Rotation))
        {
            return false;
        }

        return movementData is not ExosuitMovementData exosuit || IsFinite(exosuit.AimTargetLeft) && IsFinite(exosuit.AimTargetRight);
    }

    private static bool IsFinite(NitroxVector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static bool IsFinite(NitroxQuaternion value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z) && float.IsFinite(value.W) &&
        value.X * value.X + value.Y * value.Y + value.Z * value.Z + value.W * value.W > 0.0001f;
}
