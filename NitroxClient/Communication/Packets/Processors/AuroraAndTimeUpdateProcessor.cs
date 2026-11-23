using System.Collections.Generic;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class AuroraAndTimeUpdateProcessor(TimeManager timeManager, IPacketSender packetSender) : IClientPacketProcessor<AuroraAndTimeUpdate>
{
    private readonly TimeManager timeManager = timeManager;
    private readonly IPacketSender packetSender = packetSender;

    public Task Process(ClientProcessorContext context, AuroraAndTimeUpdate packet)
    {
        double timeBefore = DayNightCycle.main ? DayNightCycle.main.timePassedAsDouble : 0;
        timeManager.ProcessUpdate(packet.TimeData.TimePacket);
        StoryManager.UpdateAuroraData(packet.TimeData.AuroraEventData);
        timeManager.AuroraRealExplosionTime = packet.TimeData.AuroraEventData.AuroraRealExplosionTime;
        if (packet.Restore)
        {
            // A game still loading gets the restored story with its snapshot
            if (StoryManager.IsStoryLoaded() && CrashedShipExploder.main)
            {
                List<string> followUps = StoryManager.RestoreAurora();
                // The server keeps the report of the game whose player restored the Aurora
                packetSender.Send(new AuroraFollowUpGoalsCleared(followUps));
            }
        }
        else
        {
            // A time skip comes with this packet and may pass several warnings, or the explosion
            StoryManager.ActOnAuroraEventsPassed(timeBefore);
        }
        return Task.CompletedTask;
    }
}
