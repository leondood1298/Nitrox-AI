using Nitrox.Server.Subnautica.Models.GameLogic.Players;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class SignalPingPreferenceChangedProcessor(ILogger<SignalPingPreferenceChangedProcessor> logger) : IAuthPacketProcessor<SignalPingPreferenceChanged>
{
    private readonly ILogger<SignalPingPreferenceChangedProcessor> logger = logger;

    public Task Process(AuthProcessorContext context, SignalPingPreferenceChanged packet)
    {
        if (!PingPreferenceAuthority.TryAccept(context.Sender.PingInstancePreferences, packet.PingKey, packet.Visible, packet.Color))
        {
            logger.ZLogWarningOnce($"Player {context.Sender.Name} sent a ping preference that was not kept (key length {packet.PingKey?.Length ?? 0}, {context.Sender.PingInstancePreferences.Count} kept)");
        }
        return Task.CompletedTask;
    }
}
