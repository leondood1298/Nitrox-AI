using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     Passes the smell a player left on an item they dropped to the other players: whoever simulates a Stalker that picks
///     the item up needs it.
/// </summary>
internal sealed class DroppedItemSmellProcessor : IAuthPacketProcessor<DroppedItemSmell>
{
    /// <summary>
    ///     The smell Pickupable.Drop leaves.
    /// </summary>
    private const float MAX_STRENGTH = 1f;

    public async Task Process(AuthProcessorContext context, DroppedItemSmell packet)
    {
        if (IsValid(packet, context.Sender))
        {
            await context.SendToOthersAsync(packet);
        }
    }

    internal static bool IsValid(DroppedItemSmell packet, Player sender)
    {
        // A player only leaves their own smell, or they could make Stalkers befriend anybody
        return packet.PlayerId == sender.GameObjectId && packet.Strength > 0f && packet.Strength <= MAX_STRENGTH && packet.Falloff > 0f &&
               !float.IsInfinity(packet.Falloff);
    }
}
