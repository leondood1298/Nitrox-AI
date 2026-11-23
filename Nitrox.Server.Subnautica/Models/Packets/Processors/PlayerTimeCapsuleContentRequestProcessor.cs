using System.Collections.Generic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     Sends a rocket's time capsule as it is now to a player who opens it, when that player's game showed another
///     rocket's capsule before. The items of a capsule nobody shows stay its children here.
/// </summary>
internal sealed class PlayerTimeCapsuleContentRequestProcessor(EntityRegistry entityRegistry, ILogger<PlayerTimeCapsuleContentRequestProcessor> logger)
    : IAuthPacketProcessor<PlayerTimeCapsuleContentRequest>
{
    private readonly EntityRegistry entityRegistry = entityRegistry;
    private readonly ILogger<PlayerTimeCapsuleContentRequestProcessor> logger = logger;

    public async Task Process(AuthProcessorContext context, PlayerTimeCapsuleContentRequest packet)
    {
        if (!entityRegistry.TryGetEntityById(packet.CapsuleId, out PlayerTimeCapsuleEntity? capsule))
        {
            logger.ZLogWarning($"Player {context.Sender.Name} asked for the unknown time capsule {packet.CapsuleId}");
            return;
        }

        PlayerTimeCapsuleMetadata text = capsule.Metadata as PlayerTimeCapsuleMetadata ?? new PlayerTimeCapsuleMetadata("", "");
        List<Entity> items = [.. capsule.ChildEntities];
        await context.ReplyAsync(new PlayerTimeCapsuleContent(capsule.Id, text, items));
    }
}
