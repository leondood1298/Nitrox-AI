using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class RocketLaunchRefusedProcessor(Rockets rockets) : IClientPacketProcessor<RocketLaunchRefused>
{
    private readonly Rockets rockets = rockets;

    public Task Process(ClientProcessorContext context, RocketLaunchRefused packet)
    {
        rockets.RocketLaunchRefused(packet.PlayersNotAboard);
        return Task.CompletedTask;
    }
}
