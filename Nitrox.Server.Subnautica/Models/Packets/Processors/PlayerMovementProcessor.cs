using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class PlayerMovementProcessor(EntityRegistry entityRegistry) : IAuthPacketProcessor<PlayerMovement>
{
    private readonly EntityRegistry entityRegistry = entityRegistry;

    public async Task Process(AuthProcessorContext context, PlayerMovement packet)
    {
        // A place that is not a number would be stored, saved and passed on to every other game.
        if (!packet.Position.IsFinite() || !packet.BodyRotation.IsFinite())
        {
            return;
        }
        Optional<PlayerEntity> playerEntity = entityRegistry.GetEntityById<PlayerEntity>(context.Sender.PlayerContext.PlayerNitroxId);

        if (playerEntity.HasValue)
        {
            playerEntity.Value.Transform.Position = packet.Position;
            playerEntity.Value.Transform.Rotation = packet.BodyRotation;
        }

        context.Sender.Position = packet.Position;
        context.Sender.Rotation = packet.BodyRotation;
        await context.SendToOthersAsync(packet);
    }
}
