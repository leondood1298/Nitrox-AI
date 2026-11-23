using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     From a client: one more scan of <see cref="TechType" /> finished. From the server, to every player including the
///     scanning one: the scan progress the server counted.
/// </summary>
[Serializable]
public class PDAScanFinished : Packet
{
    public NitroxId? Id { get; }
    public NitroxTechType TechType { get; }

    /// <summary>
    ///     Set by the server: how many fragments of the tech have been scanned. Clients set it to 0.
    /// </summary>
    public int UnlockedAmount { get; }

    /// <summary>
    ///     How many scans research the tech. Sent by the client because the server does not have PDAScanner's data.
    /// </summary>
    public int TotalFragments { get; }

    /// <summary>
    ///     Set by the server once <see cref="UnlockedAmount" /> reached <see cref="TotalFragments" />.
    /// </summary>
    public bool FullyResearched { get; }

    public bool Destroy { get; }
    public bool WasAlreadyResearched { get; }

    public PDAScanFinished(NitroxId? id, NitroxTechType techType, int unlockedAmount, int totalFragments, bool fullyResearched, bool destroy, bool wasAlreadyResearched = false)
    {
        Id = id;
        TechType = techType;
        UnlockedAmount = unlockedAmount;
        TotalFragments = totalFragments;
        FullyResearched = fullyResearched;
        Destroy = destroy;
        WasAlreadyResearched = wasAlreadyResearched;
    }
}
