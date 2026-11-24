using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     A door of a Cyclops was opened or closed. Any player may do so; the state is kept on the door's entity, so that a
///     player who joins later sees the door as the others do, and relayed to the others as it was sent.
/// </summary>
internal sealed class OpenableStateChangedProcessor(EntityRegistry entityRegistry) : IAuthPacketProcessor<OpenableStateChanged>
{
    private readonly EntityRegistry entityRegistry = entityRegistry;

    public async Task Process(AuthProcessorContext context, OpenableStateChanged packet)
    {
        if (entityRegistry.TryGetEntityById(packet.Id, out Entity? door))
        {
            TryStore(door, packet.IsOpen);
        }
        await context.SendToOthersAsync(packet);
    }

    /// <summary>
    ///     An entity has one metadata slot: a door's state never takes the place of other metadata.
    /// </summary>
    /// <returns>Whether the state was stored.</returns>
    internal static bool TryStore(Entity door, bool isOpen)
    {
        if (door.Metadata is not (null or OpenableMetadata))
        {
            return false;
        }
        door.Metadata = new OpenableMetadata(isOpen);
        return true;
    }
}
