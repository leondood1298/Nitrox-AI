using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class SandSharkBurrowChangedProcessor(SandSharkBurrows sandSharkBurrows) : IClientPacketProcessor<SandSharkBurrowChanged>
{
    private readonly SandSharkBurrows sandSharkBurrows = sandSharkBurrows;

    public Task Process(ClientProcessorContext context, SandSharkBurrowChanged packet)
    {
        sandSharkBurrows.Apply(packet);
        return Task.CompletedTask;
    }
}
