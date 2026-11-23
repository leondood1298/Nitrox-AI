using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     Remembers who sits where, so that a player joining later sees them seated and cannot deconstruct their bench.
/// </summary>
internal sealed class BenchChangedProcessor(PlayerHandAndSeatStates handAndSeatStates) : IAuthPacketProcessor<BenchChanged>
{
    private readonly PlayerHandAndSeatStates handAndSeatStates = handAndSeatStates;

    public async Task Process(AuthProcessorContext context, BenchChanged packet)
    {
        // The others would seat whichever player the packet names
        if (packet.SessionId != context.Sender.SessionId)
        {
            return;
        }

        handAndSeatStates.OnBenchChanged(context.Sender.SessionId, packet);
        await context.SendToOthersAsync(packet);
    }
}
