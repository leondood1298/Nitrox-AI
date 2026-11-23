using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     Gives a medical cabinet one timer for all players: each game starts its own when it loads a cabinet, at a different
///     moment, so the first medkit would appear at different times.
/// </summary>
internal sealed class MedicalCabinetTimerStartedProcessor(PlayerManager playerManager, EntityRegistry entityRegistry, ILogger<MedicalCabinetTimerStartedProcessor> logger)
    : IAuthPacketProcessor<MedicalCabinetTimerStarted>
{
    private readonly PlayerManager playerManager = playerManager;
    private readonly EntityRegistry entityRegistry = entityRegistry;
    private readonly ILogger<MedicalCabinetTimerStartedProcessor> logger = logger;

    public async Task Process(AuthProcessorContext context, MedicalCabinetTimerStarted packet)
    {
        if (!entityRegistry.TryGetEntityById(packet.Id, out Entity entity))
        {
            logger.ZLogDebug($"Player {context.Sender.Name} reported the timer of medical cabinet {packet.Id}, which the server does not know");
            return;
        }
        // The Lifepod's cabinet and a cabinet built in a base both carry the cabinet's tech type.
        if (entity.TechType?.Name != "MedicalCabinet")
        {
            logger.ZLogWarningOnce($"Player {context.Sender.Name} reported a medical cabinet timer for {entity.TechType?.Name ?? "None"} entity {entity.Id}");
            return;
        }

        if (entity.Metadata is MedicalCabinetMetadata stored)
        {
            await context.ReplyAsync(new EntityMetadataUpdate(entity.Id, stored));
            return;
        }

        entity.Metadata = packet.State;
        EntityMetadataUpdate update = new(entity.Id, packet.State);
        foreach (Player player in playerManager.GetConnectedPlayers())
        {
            if (player != context.Sender && player.CanSee(entity))
            {
                await context.SendAsync(update, player.SessionId);
            }
        }
    }
}
