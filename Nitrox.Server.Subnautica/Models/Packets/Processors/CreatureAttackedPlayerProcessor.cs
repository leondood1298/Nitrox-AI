using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     Passes a creature's hit on a player from the creature's simulating player to the player who was hit.
/// </summary>
internal sealed class CreatureAttackedPlayerProcessor(IPacketSender packetSender, PlayerManager playerManager, SimulationOwnershipData simulationOwnershipData)
    : IAuthPacketProcessor<CreatureAttackedPlayer>
{
    private readonly IPacketSender packetSender = packetSender;
    private readonly PlayerManager playerManager = playerManager;
    private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;

    public async Task Process(AuthProcessorContext context, CreatureAttackedPlayer packet)
    {
        if (!IsReportFromSimulatingPlayer(packet, simulationOwnershipData.GetPlayerForLock(packet.CreatureId), context.Sender))
        {
            return;
        }
        if (!playerManager.TryGetPlayerBySessionId(packet.TargetSessionId, out Player targetPlayer))
        {
            return;
        }

        await packetSender.SendPacketAsync(packet, targetPlayer.SessionId);
    }

    /// <summary>
    ///     Only the player simulating a creature knows what it attacked. Anyone else's report would let them hurt other
    ///     players at will.
    /// </summary>
    internal static bool IsReportFromSimulatingPlayer(CreatureAttackedPlayer packet, Player? simulatingPlayer, Player sender)
    {
        if (!(packet.OriginalDamage > 0f) || float.IsInfinity(packet.OriginalDamage))
        {
            return false;
        }

        // A hit on the simulating player themselves is applied by their own game
        if (packet.TargetSessionId == sender.SessionId)
        {
            return false;
        }

        // Compared by session: Player's == compares the PeerId
        return simulatingPlayer != null && simulatingPlayer.SessionId == sender.SessionId;
    }
}
