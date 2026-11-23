using System.Threading.Tasks;
using Nitrox.Model.Packets.Core;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class WelderUseProcessor(PlayerManager playerManager) : IClientPacketProcessor<WelderUse>
{
    private readonly PlayerManager playerManager = playerManager;

    public Task Process(ClientProcessorContext context, WelderUse packet)
    {
        if (!playerManager.TryFind(packet.PlayerId, out RemotePlayer remotePlayer) || !remotePlayer.ItemAttachPoint)
        {
            return Task.CompletedTask;
        }
        Welder componentInChildren = remotePlayer.ItemAttachPoint.GetComponentInChildren<Welder>(includeInactive: true);
        if (!componentInChildren)
        {
            return Task.CompletedTask;
        }
        if (componentInChildren.weldSound)
        {
            if (packet.Welding)
            {
                componentInChildren.weldSound.Play();
            }
            else
            {
                componentInChildren.weldSound.Stop();
            }
        }
        if (componentInChildren.fxControl)
        {
            if (packet.Welding && MiscSettings.flashes)
            {
                int i = (remotePlayer.AnimationController["is_underwater"] ? 1 : 0);
                componentInChildren.fxControl.Play(i);
            }
            else
            {
                componentInChildren.fxControl.StopAndDestroy(0f);
            }
        }
        return Task.CompletedTask;
    }
}
