using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     Passes a player's hand-feeding of a Stalker to the player who simulates the Stalker.
/// </summary>
internal sealed class StalkerFedByPlayerProcessor(SimulationOwnershipData simulationOwnershipData) : IAuthPacketProcessor<StalkerFedByPlayer>
{
    /// <summary>
    ///     The most a fish feeds (MeleeAttack.TryEat, a shark-sized one).
    /// </summary>
    private const float MAX_FOOD_VALUE = 3f;

    private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;

    public async Task Process(AuthProcessorContext context, StalkerFedByPlayer packet)
    {
        Player? simulatingPlayer = simulationOwnershipData.GetPlayerForLock(packet.StalkerId);
        if (simulatingPlayer == null || !IsValid(packet, simulatingPlayer, context.Sender))
        {
            return;
        }
        await context.SendAsync(packet, simulatingPlayer.SessionId);
    }

    internal static bool IsValid(StalkerFedByPlayer packet, Player simulatingPlayer, Player sender)
    {
        // A player only feeds from their own hand; the simulating player's own feeding never leaves its game
        return packet.PlayerId == sender.GameObjectId && simulatingPlayer.SessionId != sender.SessionId &&
               packet.FoodValue > 0f && packet.FoodValue <= MAX_FOOD_VALUE;
    }
}
