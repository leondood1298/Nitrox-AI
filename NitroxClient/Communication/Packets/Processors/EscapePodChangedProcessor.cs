using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class EscapePodChangedProcessor(PlayerManager remotePlayerManager) : IClientPacketProcessor<EscapePodChanged>
{
    private readonly PlayerManager remotePlayerManager = remotePlayerManager;

    public Task Process(ClientProcessorContext context, EscapePodChanged packet)
    {
        Optional<RemotePlayer> remotePlayer = remotePlayerManager.Find(packet.SessionId);

        if (remotePlayer.HasValue)
        {
            EscapePod? escapePod = null;

            // A pod this player has not loaded cannot hold the other player's model; it is then shown on its own.
            if (packet.EscapePodId.HasValue && NitroxEntity.TryGetObjectFrom(packet.EscapePodId.Value, out GameObject? pod))
            {
                escapePod = pod.GetComponent<EscapePod>();
            }
            else if (packet.EscapePodId.HasValue)
            {
                Log.Debug($"[{nameof(EscapePodChangedProcessor)}] Escape pod {packet.EscapePodId.Value} is not loaded here");
            }

            remotePlayer.Value.SetEscapePod(escapePod);
        }
        return Task.CompletedTask;
    }
}
