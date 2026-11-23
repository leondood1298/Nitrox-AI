using System;
using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     Tells a player that an operation of theirs on a base was refused, with the operation every base is at.
/// </summary>
[Serializable]
public sealed class BuildingDesyncWarning : Packet
{
    public Dictionary<NitroxId, int> Operations { get; }

    /// <summary>
    ///     The base the refused operation was on, or null when it is not known.
    /// </summary>
    public NitroxId? BaseId { get; }

    public BuildingDesyncWarning(Dictionary<NitroxId, int> operations, NitroxId? baseId)
    {
        Operations = operations;
        BaseId = baseId;
    }
}
