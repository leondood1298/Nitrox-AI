using System.Collections.Generic;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class VehicleMovementsPacketProcessor(VehicleAuthority vehicleAuthority,
    VehicleDiagnostics diagnostics, ILogger<VehicleMovementsPacketProcessor> logger) : IAuthPacketProcessor<VehicleMovements>
{
    private static readonly NitroxVector3 CyclopsSteeringWheelRelativePosition = new(-0.05f, 0.97f, -23.54f);

    public async Task Process(AuthProcessorContext context, VehicleMovements packet)
    {
        if (!VehicleAuthority.IsValidMovementCount(packet.Data.Count) || !double.IsFinite(packet.RealTime))
        {
            diagnostics.RecordMovement(false);
            logger.ZLogWarning($"[Vehicle] rejected malformed movement packet from {context.Sender.Name} #{context.Sender.SessionId}: count={packet.Data.Count}, time={packet.RealTime}");
            return;
        }

        List<MovementData> acceptedMovements = new(packet.Data.Count);
        foreach (MovementData movementData in packet.Data)
        {
            bool hasFiniteTransform = VehicleAuthority.IsFinite(movementData);
            if (!hasFiniteTransform)
            {
                diagnostics.RecordMovement(false);
                logger.ZLogWarningOnce($"[Vehicle] rejected movement for {movementData.Id} from {context.Sender.Name} #{context.Sender.SessionId}: non-finite transform");
                continue;
            }

            if (!vehicleAuthority.TryGetOwnedWorldEntity(context.Sender, movementData.Id, false, out WorldEntity worldEntity, out string rejectionReason))
            {
                diagnostics.RecordMovement(false);
                logger.ZLogWarningOnce($"[Vehicle] rejected movement for {movementData.Id} from {context.Sender.Name} #{context.Sender.SessionId}: {rejectionReason}");
                continue;
            }

            if (movementData is DrivenVehicleMovementData && context.Sender.PlayerContext?.DrivingVehicle != movementData.Id)
            {
                diagnostics.RecordMovement(false);
                logger.ZLogWarningOnce($"[Vehicle] rejected driven movement for {movementData.Id} from {context.Sender.Name} #{context.Sender.SessionId}: sender is not the canonical pilot");
                continue;
            }

            worldEntity.Transform.Position = movementData.Position;
            worldEntity.Transform.Rotation = movementData.Rotation;

            if (movementData is DrivenVehicleMovementData)
            {
                if (worldEntity.TechType.Name == "Cyclops")
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

            diagnostics.RecordMovement(true);
            acceptedMovements.Add(movementData);
            if (diagnostics.TraceEnabled)
            {
                logger.ZLogInformation($"[Vehicle] accepted movement for {movementData.Id} from {context.Sender.Name} #{context.Sender.SessionId}");
            }
        }

        if (acceptedMovements.Count > 0)
        {
            await context.SendToOthersAsync(new VehicleMovements(acceptedMovements, packet.RealTime));
        }
    }
}
