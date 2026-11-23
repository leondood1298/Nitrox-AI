using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class SubRootChangedPacketProcessor(EntityRegistry entityRegistry, ILogger<SubRootChangedPacketProcessor> logger) : IAuthPacketProcessor<SubRootChanged>
{
    private readonly EntityRegistry entityRegistry = entityRegistry;
    private readonly ILogger<SubRootChangedPacketProcessor> logger = logger;

    public async Task Process(AuthProcessorContext context, SubRootChanged packet)
    {
        // The others would move whichever player the packet names into the sub.
        if (packet.SessionId != context.Sender.SessionId)
        {
            logger.ZLogDebug($"Ignored {nameof(SubRootChanged)} from {context.Sender.Name} that named session #{packet.SessionId}");
            return;
        }
        // The player entity would be put under it and saved there.
        if (packet.SubRootId.HasValue && !IsSubRootFor(context.Sender, packet.SubRootId.Value))
        {
            logger.ZLogDebug($"Ignored {nameof(SubRootChanged)} from {context.Sender.Name} into {packet.SubRootId.Value}, which is no base or Cyclops the player can be in");
            return;
        }

        entityRegistry.ReparentEntity(context.Sender.GameObjectId, packet.SubRootId.OrNull());
        context.Sender.SubRootId = packet.SubRootId;
        await context.SendToOthersAsync(packet);
    }

    /// <summary>
    ///     A client names the object that holds the game's SubRoot: a base, whose root is a <see cref="BuildEntity" />,
    ///     or a Cyclops, which is a <see cref="VehicleEntity" />. Under itself or under something it carries the player
    ///     would become its own ancestor.
    /// </summary>
    private bool IsSubRootFor(Player player, NitroxId subRootId)
    {
        if (!entityRegistry.TryGetEntityById(subRootId, out Entity subRoot))
        {
            return false;
        }
        if (subRoot is not BuildEntity && subRoot is not VehicleEntity { TechType.Name: "Cyclops" })
        {
            return false;
        }
        return !entityRegistry.IsSelfOrDescendantOf(subRoot, player.GameObjectId);
    }
}
