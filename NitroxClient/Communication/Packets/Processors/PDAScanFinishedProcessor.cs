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
            bool wasComplete = PDAScanner.complete.Contains(packetTechType);
            PDAScanner.partial.RemoveAllFast(packetTechType, static (item, techType) => item.techType == techType);
            PDAScanner.complete.Add(packetTechType);
            // When two players scan the last fragments at the same time, no game counts up to the total itself, so no game
            // unlocked the blueprint. The scanning game unlocked it already when it did count up to the total.
            if (!wasComplete)
            {
                PDAScanner.EntryData entryData = PDAScanner.GetEntryData(packetTechType);
                if (entryData != null)
                {
                    PDAScanner.Unlock(entryData, true, true, true);
                }
            }
            return Task.CompletedTask;
        }
        // PDAScanner.Add only raises an existing entry's count, the server's count is set as it is
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
