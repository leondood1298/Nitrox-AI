using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

/// <summary>
///     Another player got an item first that this game had given to its player (see <see cref="ItemClaims" />).
/// </summary>
internal sealed class ItemClaimLostProcessor(ItemClaims itemClaims) : IClientPacketProcessor<ItemClaimLost>
{
    private readonly ItemClaims itemClaims = itemClaims;

    public Task Process(ClientProcessorContext context, ItemClaimLost packet)
    {
        itemClaims.Lost(packet);
        return Task.CompletedTask;
    }
}
