using System;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     A precursor teleporter pair was activated. Both ends share the identifier, and a teleporter that spawns later asks
///     the game's TeleporterManager whether its identifier is active.
/// </summary>
[Serializable]
public class TeleporterActivated : Packet
{
    public string TeleporterIdentifier { get; }

    public TeleporterActivated(string teleporterIdentifier)
    {
        TeleporterIdentifier = teleporterIdentifier;
    }
}
