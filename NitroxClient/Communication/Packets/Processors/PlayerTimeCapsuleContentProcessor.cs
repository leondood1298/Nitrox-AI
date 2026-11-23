using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class PlayerTimeCapsuleContentProcessor(TimeCapsules timeCapsules) : IClientPacketProcessor<PlayerTimeCapsuleContent>
{
    private readonly TimeCapsules timeCapsules = timeCapsules;

    public Task Process(ClientProcessorContext context, PlayerTimeCapsuleContent packet)
    {
        timeCapsules.ShowContent(packet);
        return Task.CompletedTask;
    }
}
