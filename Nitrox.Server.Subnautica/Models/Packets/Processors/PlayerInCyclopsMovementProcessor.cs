using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class PlayerInCyclopsMovementProcessor(EntityRegistry entityRegistry, VehicleAuthority vehicleAuthority,
    VehicleDiagnostics diagnostics, ILogger<PlayerInCyclopsMovementProcessor> logger) : IAuthPacketProcessor<PlayerInCyclopsMovement>
{
    public async Task Process(AuthProcessorContext context, PlayerInCyclopsMovement packet)
    {
        if (context.Sender.PlayerContext == null ||
            !entityRegistry.TryGetEntityById(context.Sender.PlayerContext.PlayerNitroxId, out PlayerEntity playerEntity))
        {
            diagnostics.RecordMovement(false);
            logger.ZLogErrorOnce($"{nameof(PlayerEntity)} couldn't be found for player {context.Sender.Name}. It is advised the player reconnects before losing too much progression.");
            return;
        }

        if (!context.Sender.SubRootId.HasValue ||
            !vehicleAuthority.IsCyclops(context.Sender.SubRootId.Value) ||
            playerEntity.ParentId != context.Sender.SubRootId.Value ||
            !VehicleAuthority.IsFiniteTransform(packet.LocalPosition, packet.LocalRotation))
        {
            diagnostics.RecordMovement(false);
            logger.ZLogWarningOnce($"[Vehicle] rejected in-Cyclops movement from {context.Sender.Name} #{context.Sender.SessionId}: player is not canonically parented to a Cyclops or transform is invalid");
            return;
        }

        playerEntity.Transform.LocalPosition = packet.LocalPosition;
        playerEntity.Transform.LocalRotation = packet.LocalRotation;
        context.Sender.Position = playerEntity.Transform.Position;
        context.Sender.Rotation = playerEntity.Transform.Rotation;
        diagnostics.RecordMovement(true);

        await context.SendToOthersAsync(new PlayerInCyclopsMovement(context.Sender.SessionId, packet.LocalPosition, packet.LocalRotation));
    }
}
