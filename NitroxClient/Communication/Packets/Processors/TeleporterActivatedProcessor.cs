using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class TeleporterActivatedProcessor(PrecursorTeleporters precursorTeleporters) : IClientPacketProcessor<TeleporterActivated>
{
    private readonly PrecursorTeleporters precursorTeleporters = precursorTeleporters;

    public Task Process(ClientProcessorContext context, TeleporterActivated packet)
    {
        precursorTeleporters.ActivateFromRemote(packet.TeleporterIdentifier);
        return Task.CompletedTask;
    }
}
