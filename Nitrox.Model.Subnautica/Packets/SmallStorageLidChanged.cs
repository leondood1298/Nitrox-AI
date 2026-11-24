using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     A player opened or closed the lid of a waterproof locker. It carries the state, not a toggle, so that a game that
///     missed a change does not show the lid the wrong way round from then on.
/// </summary>
[Serializable]
public sealed class SmallStorageLidChanged : Packet
{
    public NitroxId Id { get; }
    public bool Open { get; }

    public SmallStorageLidChanged(NitroxId id, bool open)
    {
        Id = id;
        Open = open;
    }
}
