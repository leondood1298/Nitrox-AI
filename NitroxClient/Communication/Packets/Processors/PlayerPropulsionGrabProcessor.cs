using System.Threading.Tasks;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class PlayerPropulsionGrabProcessor(PlayerManager playerManager) : IClientPacketProcessor<PlayerPropulsionGrab>
{
    private readonly PlayerManager playerManager = playerManager;

    public Task Process(ClientProcessorContext context, PlayerPropulsionGrab packet)
    {
        if (playerManager.TryFind(packet.SessionId, out RemotePlayer remotePlayer))
        {
            remotePlayer.SetPropulsionGrab(packet.GrabbedId);
        }
        return Task.CompletedTask;
    }
}
