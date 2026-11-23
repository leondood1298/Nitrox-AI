using System;
using System.Collections.Generic;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace Nitrox.Model.Subnautica.Packets
{
    [Serializable]
    public class KnownTechEntryAdd : Packet
    {
        public enum EntryCategory
        {
            /// <summary>
            /// <b>KnownTech.knownTech</b><br/>
            /// Tech items that you learn about after fulfilling a requirement. Often times, discovering a new KnownTech.analyzedTech item will provide 1 or more KnownTech.knownTech items. (Peeper -> CookedPeeper)
            /// </summary>
            KNOWN = 0,
            /// <summary>
            /// <b>KnownTech.analyzedTech</b><br/>
            /// Tech items that you find in the world and acquire. They often show a notification saying you found/learned about them. (Fish, Resources, etc)
            /// <br/>
            /// </summary>
            ANALYZED = 1
        }

        public NitroxTechType TechType { get; }
        public bool Verbose { get; }
        public EntryCategory Category { get; }
        /// <summary>
        ///     Scanner entries whose blueprint is <see cref="TechType" />. The game marks them as completely scanned when the
        ///     blueprint is learned (PDAScanner.CompleteAllEntriesWhichUnlocks), the server stores that.
        /// </summary>
        public List<NitroxTechType> CompletedScannerEntries { get; }

        public KnownTechEntryAdd(EntryCategory category, NitroxTechType techType, bool verbose, List<NitroxTechType>? completedScannerEntries = null)
        {
            Category = category;
            TechType = techType;
            Verbose = verbose;
            CompletedScannerEntries = completedScannerEntries ?? [];
        }
    }
}
