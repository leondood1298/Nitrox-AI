using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class SandSharkCoverStartedProcessor(SandSharkBurrows sandSharkBurrows) : IClientPacketProcessor<SandSharkCoverStarted>
{
    private readonly SandSharkBurrows sandSharkBurrows = sandSharkBurrows;

    public Task Process(ClientProcessorContext context, SandSharkCoverStarted packet)
    {
        sandSharkBurrows.ApplyCoverStarted(packet);
        return Task.CompletedTask;
    }
}
