using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.PlayerLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class SetIntroCinematicModeProcessor(PlayerManager playerManager, PlayerCinematics playerCinematics, LocalPlayer localPlayer) : IClientPacketProcessor<SetIntroCinematicMode>
{
    private readonly LocalPlayer localPlayer = localPlayer;
    private readonly PlayerCinematics playerCinematics = playerCinematics;
    private readonly PlayerManager playerManager = playerManager;

    public Task Process(ClientProcessorContext context, SetIntroCinematicMode packet)
    {
        if (localPlayer.SessionId == packet.SessionId)
        {
            // The server sends WAITING when the partner it paired this player with left before both played. An intro
            // this game has started already plays on alone; one that has not waits for another partner, or for the
            // player to start it alone.
            if (packet.Mode == IntroCinematicMode.WAITING)
            {
                if (localPlayer.IntroCinematicMode == IntroCinematicMode.START)
                {
                    playerCinematics.IntroCinematicPartnerId = null;
                    localPlayer.IntroCinematicMode = IntroCinematicMode.WAITING;
                }
                return Task.CompletedTask;
            }

            if (!TakesMode(localPlayer.IntroCinematicMode, packet.Mode))
            {
                Log.Debug($"Ignored intro mode {packet.Mode} from the server, this game's intro is {localPlayer.IntroCinematicMode}");
                return Task.CompletedTask;
            }

            if (packet.PartnerId.HasValue)
            {
                playerCinematics.IntroCinematicPartnerId = packet.PartnerId;
            }

            localPlayer.IntroCinematicMode = packet.Mode;
            return Task.CompletedTask;
        }

        if (playerManager.TryFind(packet.SessionId, out RemotePlayer remotePlayer))
        {
            remotePlayer.PlayerContext.IntroCinematicMode = packet.Mode;
            return Task.CompletedTask;
        }

        Log.Debug($"SetIntroCinematicMode couldn't find Player with id {packet.SessionId}. This is normal if player has not yet officially joined.");
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Whether this game takes a mode the server gives its player. A START is only for a game that waits for a partner:
    ///     the player may have chosen to play alone at the moment the server paired it, and the server then lets the
    ///     partner wait again (IntroCinematicPairing.ReleasePartnerAsync).
    /// </summary>
    internal static bool TakesMode(IntroCinematicMode current, IntroCinematicMode given)
    {
        return given != IntroCinematicMode.START || current == IntroCinematicMode.WAITING;
    }
}
