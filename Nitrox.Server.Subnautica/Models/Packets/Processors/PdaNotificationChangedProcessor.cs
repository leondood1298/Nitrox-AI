using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class PdaNotificationChangedProcessor(PdaNotificationManager pdaNotificationManager) : IAuthPacketProcessor<PdaNotificationChanged>
{
    private readonly PdaNotificationManager pdaNotificationManager = pdaNotificationManager;

    public Task Process(AuthProcessorContext context, PdaNotificationChanged packet)
    {
        pdaNotificationManager.Change(context.Sender, packet.Notification, packet.Unread);
        return Task.CompletedTask;
    }
}
