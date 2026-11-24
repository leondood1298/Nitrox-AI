using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class CyclopsDecoyUpdateProcessor(CyclopsDecoys cyclopsDecoys) : IClientPacketProcessor<CyclopsDecoyUpdate>
{
    private readonly CyclopsDecoys cyclopsDecoys = cyclopsDecoys;

    public Task Process(ClientProcessorContext context, CyclopsDecoyUpdate packet)
    {
        cyclopsDecoys.ApplyRemote(packet);
        return Task.CompletedTask;
    }
}
