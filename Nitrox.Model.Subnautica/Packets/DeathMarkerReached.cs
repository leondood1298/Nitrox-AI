using System;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     The player came back to where they died: the beacon there is gone, also after they rejoin.
/// </summary>
[Serializable]
public sealed class DeathMarkerReached : Packet
{
    public NitroxVector3 Position { get; }

    public DeathMarkerReached(NitroxVector3 position)
    {
        Position = position;
    }
}
