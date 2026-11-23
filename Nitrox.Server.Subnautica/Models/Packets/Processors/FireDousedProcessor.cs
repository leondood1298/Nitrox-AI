using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class FireDousedProcessor(EntitySimulation entitySimulation, WorldEntityManager worldEntityManager, EntityRegistry entityRegistry) : IAuthPacketProcessor<FireDoused>
{
    private readonly EntitySimulation entitySimulation = entitySimulation;
    private readonly WorldEntityManager worldEntityManager = worldEntityManager;
    private readonly EntityRegistry entityRegistry = entityRegistry;

    public async Task Process(AuthProcessorContext context, FireDoused packet)
    {
        if (packet.Health <= 0f)
        {
            entitySimulation.EntityDestroyed(packet.Id);
            worldEntityManager.TryDestroyEntity(packet.Id, out _);
        }
        else if (entityRegistry.TryGetEntityById(packet.Id, out Entity? fire))
        {
            // Without this a fire that was sprayed and not put out comes back at full strength for a joiner and after a
            // restart. The dousing player reports the fire's health when it starts and when it stops spraying.
            fire.Metadata = new FireMetadata(packet.Health);
        }

        await context.SendToOthersAsync(packet);
    }
}
