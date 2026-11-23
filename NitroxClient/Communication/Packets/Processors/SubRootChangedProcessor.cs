using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class SubRootChangedProcessor(PlayerManager remotePlayerManager) : IClientPacketProcessor<SubRootChanged>
{
    private readonly PlayerManager remotePlayerManager = remotePlayerManager;

    public Task Process(ClientProcessorContext context, SubRootChanged packet)
    {
        Optional<RemotePlayer> remotePlayer = remotePlayerManager.Find(packet.SessionId);

        if (remotePlayer.HasValue)
        {
            SubRoot subRoot = null;

            // A base or Cyclops this player has not loaded cannot hold the other player's model; it is then shown on its own.
            if (packet.SubRootId.HasValue && NitroxEntity.TryGetObjectFrom(packet.SubRootId.Value, out GameObject? sub))
            {
                subRoot = sub.GetComponent<SubRoot>();
            }
            else if (packet.SubRootId.HasValue)
            {
                Log.Debug($"[{nameof(SubRootChangedProcessor)}] Sub {packet.SubRootId.Value} is not loaded here");
            }

            remotePlayer.Value.SetSubRoot(subRoot);
        }
        return Task.CompletedTask;
    }
}
