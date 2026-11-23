using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class PDAScanFinishedProcessor : IClientPacketProcessor<PDAScanFinished>
{
    public Task Process(ClientProcessorContext context, PDAScanFinished packet)
    {
        if (packet.Id != null)
        {
            StoryManager.ScanCompleted(packet.Id, packet.Destroy);
        }
        if (packet.WasAlreadyResearched)
        {
            return Task.CompletedTask;
        }
        TechType packetTechType = packet.TechType.ToUnity();
        if (packet.FullyResearched)
        {
            PDAScanner.partial.RemoveAllFast(packetTechType, static (item, techType) => item.techType == techType);
            PDAScanner.complete.Add(packetTechType);
            return Task.CompletedTask;
        }
        // PDAScanner.Add always appends, so an existing partial entry must be updated in place. A second entry for the
        // same type would shadow this one and local scans would keep counting from the stale value.
        bool hadEntry = PDAScanner.GetPartialEntryByKey(packetTechType, out PDAScanner.Entry updatedEntry);
        int previousUnlocked = hadEntry ? updatedEntry.unlocked : 0;
        if (hadEntry)
        {
            updatedEntry.unlocked = packet.UnlockedAmount;
        }
        else
        {
            updatedEntry = PDAScanner.Add(packetTechType, packet.UnlockedAmount);
        }
        if (updatedEntry != null)
        {
            int totalFragments = PDAScanner.GetEntryData(packetTechType)?.totalFragments ?? 1;
            bool multiplayerActive = Multiplayer.Main;
            bool initialSyncCompleted = multiplayerActive && Multiplayer.Main.InitialSyncCompleted;
            if (ScannerProgressPopupPolicy.ShouldShow(packet.WasAlreadyResearched, packet.FullyResearched, previousUnlocked, updatedEntry.unlocked, totalFragments, multiplayerActive, initialSyncCompleted))
            {
                float percentage = Mathf.RoundToInt((float)updatedEntry.unlocked / totalFragments * 100f);
                ErrorMessage.AddError(Language.main.GetFormat("ScannerInstanceScanned", Language.main.Get(packetTechType.AsString()), percentage, updatedEntry.unlocked, totalFragments));
            }
        }
        return Task.CompletedTask;
    }
}
