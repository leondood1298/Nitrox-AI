using System;
using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic.Bases;
using NitroxClient.GameLogic.Settings;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class BuildingDesyncWarningProcessor : IClientPacketProcessor<BuildingDesyncWarning>
{
    public Task Process(ClientProcessorContext context, BuildingDesyncWarning packet)
    {
        if (!BuildingHandler.Main)
        {
            return Task.CompletedTask;
        }

        MarkDesynced(packet, BuildingHandler.Main.EnsureTracker);
        if (packet.BaseId != null)
        {
            // The refused operation was on one base: that base is fetched again at once, the menu's Resync stays for the rest
            BuildingHandler.Main.AskForBaseResync(packet.BaseId);
        }

        if (NitroxPrefs.SafeBuildingLog.Value)
        {
            Log.InGame(Language.main.Get("Nitrox_BuildingDesyncDetected"));
        }
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Marks the base of the refused operation as desynced, with the server's operation for it. Without a base, every
    ///     base is marked.
    /// </summary>
    internal static void MarkDesynced(BuildingDesyncWarning packet, Func<NitroxId, OperationTracker> ensureTracker)
    {
        foreach (KeyValuePair<NitroxId, int> operation in packet.Operations)
        {
            if (packet.BaseId != null && operation.Key != packet.BaseId)
            {
                continue;
            }
            OperationTracker tracker = ensureTracker(operation.Key);
            tracker.LastOperationId = operation.Value;
            tracker.LocalOperations = 0;  // discard locally-queued ops, server's value is now authoritative
            tracker.FailedOperations++;
        }
    }
}
