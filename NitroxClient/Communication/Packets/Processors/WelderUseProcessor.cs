using System.Threading.Tasks;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class WelderUseProcessor(PlayerManager playerManager) : IClientPacketProcessor<WelderUse>
{
    private readonly PlayerManager playerManager = playerManager;

    public Task Process(ClientProcessorContext context, WelderUse packet)
    {
        // Kept even before the welder is in the hand: a joiner is told about the welding just after the drawing,
        // which may take a few frames to show
        if (playerManager.TryFind(packet.PlayerId, out RemotePlayer remotePlayer))
        {
            remotePlayer.SetWelding(packet.Welding);
        }
        return Task.CompletedTask;
    }
}
