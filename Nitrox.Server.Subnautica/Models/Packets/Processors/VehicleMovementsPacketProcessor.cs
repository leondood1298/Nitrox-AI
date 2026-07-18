using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class VehicleMovementsPacketProcessor(EntityRegistry entityRegistry, SimulationOwnershipData simulationOwnershipData, ILogger<VehicleMovementsPacketProcessor> logger)
    : IAuthPacketProcessor<VehicleMovements>
{
    internal const int MAX_MOVEMENTS_PER_PACKET = 64;

    private static readonly NitroxVector3 CyclopsSteeringWheelRelativePosition = new(-0.05f, 0.97f, -23.54f);

    private readonly EntityRegistry entityRegistry = entityRegistry;
    private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;
    private readonly ILogger<VehicleMovementsPacketProcessor> logger = logger;

    public async Task Process(AuthProcessorContext context, VehicleMovements packet)
    {
        if (!IsValidMovementCount(packet.Data?.Count ?? 0) || !double.IsFinite(packet.RealTime))
        {
            logger.ZLogWarningOnce($"Rejected malformed movement batch from {context.Sender.Name} #{context.Sender.SessionId}");
            return;
        }

        List<(MovementData Movement, WorldEntity Entity)> candidates = new(packet.Data.Count);
        foreach (MovementData movementData in packet.Data)
        {
            if (!IsFiniteMovement(movementData))
            {
                logger.ZLogWarningOnce($"Rejected non-finite movement entry from {context.Sender.Name} #{context.Sender.SessionId}");
                continue;
            }

            if (!entityRegistry.TryGetEntityById(movementData.Id, out WorldEntity worldEntity) || worldEntity.Transform == null)
            {
                logger.ZLogWarningOnce($"Player {context.Sender.Name} tried updating unknown or non-world entity {movementData.Id}");
                continue;
            }

            candidates.Add((movementData, worldEntity));
        }

        int unauthorizedCount = 0;
        int nonPilotCount = 0;
        Task sendTask = simulationOwnershipData.ExecuteForOwner(context.Sender,
            candidates.Select(candidate => candidate.Movement.Id), ownedIds =>
            {
                List<MovementData> acceptedMovements = new(candidates.Count);
                foreach ((MovementData movementData, WorldEntity worldEntity) in candidates)
                {
                    if (!ownedIds.Contains(movementData.Id))
                    {
                        unauthorizedCount++;
                        continue;
                    }
                    if (movementData is DrivenVehicleMovementData && context.Sender.PlayerContext?.DrivingVehicle != movementData.Id)
                    {
                        nonPilotCount++;
                        continue;
                    }

                    worldEntity.Transform.Position = movementData.Position;
                    worldEntity.Transform.Rotation = movementData.Rotation;

                    if (movementData is DrivenVehicleMovementData)
                    {
                        // Cyclops' driving wheel is at a known position so we need to adapt the position of the player accordingly
                        if (string.Equals(worldEntity.TechType?.Name, "Cyclops", StringComparison.Ordinal))
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
        if (nonPilotCount > 0)
        {
            logger.ZLogWarningOnce($"Rejected {nonPilotCount} driven movement entr{(nonPilotCount == 1 ? "y" : "ies")} from {context.Sender.Name}: sender is not the canonical pilot");
        }
    }

    internal static bool IsValidMovementCount(int count) => count is > 0 and <= MAX_MOVEMENTS_PER_PACKET;

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
