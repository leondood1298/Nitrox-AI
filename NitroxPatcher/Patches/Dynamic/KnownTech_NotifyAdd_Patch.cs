using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.Packets;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class KnownTech_NotifyAdd_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method(() => KnownTech.NotifyAdd(default, default));

    public static void Prefix(TechType techType, bool verbose)
    {
        if (!Multiplayer.Main || !Multiplayer.Main.InitialSyncCompleted)
        {
            return;
        }

        Resolve<IPacketSender>().Send(new KnownTechEntryAdd(
            KnownTechEntryAdd.EntryCategory.KNOWN,
            techType.ToDto(),
            verbose,
            GetScannerEntriesCompletedBy(techType)
        ));
    }

    /// <summary>
    ///     The entries PDAScanner.CompleteAllEntriesWhichUnlocks marks as complete right after this notification.
    /// </summary>
    private static List<NitroxTechType> GetScannerEntriesCompletedBy(TechType techType)
    {
        List<PDAScanner.Entry> partialEntries = [];
        PDAScanner.GetPartialEntriesWhichUnlocks(techType, partialEntries, true);

        HashSet<TechType> scannerEntries = [..partialEntries.Select(entry => entry.techType)];
        foreach (KeyValuePair<TechType, PDAScanner.EntryData> mapping in PDAScanner.mapping)
        {
            if (mapping.Value.blueprint == techType)
            {
                scannerEntries.Add(mapping.Key);
            }
        }
        return scannerEntries.Select(scannerEntry => scannerEntry.ToDto()).ToList();
    }
}
