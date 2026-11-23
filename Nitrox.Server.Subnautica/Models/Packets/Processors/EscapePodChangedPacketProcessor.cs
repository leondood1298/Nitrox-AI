using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class EscapePodChangedPacketProcessor(EntityRegistry entityRegistry, ILogger<EscapePodChangedPacketProcessor> logger) : IAuthPacketProcessor<EscapePodChanged>
{
    private readonly EntityRegistry entityRegistry = entityRegistry;
    private readonly ILogger<EscapePodChangedPacketProcessor> logger = logger;

    public async Task Process(AuthProcessorContext context, EscapePodChanged packet)
    {
        // The others would move whichever player the packet names into the pod.
        if (packet.SessionId != context.Sender.SessionId)
        {
            logger.ZLogDebug($"Ignored {nameof(EscapePodChanged)} from {context.Sender.Name} that named session #{packet.SessionId}");
            return;
        }
        // The player entity would be put under it and saved there.
        if (packet.EscapePodId.HasValue && !entityRegistry.TryGetEntityById(packet.EscapePodId.Value, out EscapePodEntity _))
        {
            logger.ZLogDebug($"Ignored {nameof(EscapePodChanged)} from {context.Sender.Name} into {packet.EscapePodId.Value}, which is no escape pod the server knows");
            return;
        }

        entityRegistry.ReparentEntity(context.Sender.GameObjectId, packet.EscapePodId.OrNull());
        context.Sender.SubRootId = packet.EscapePodId;
        await context.SendToOthersAsync(packet);
    }
}
