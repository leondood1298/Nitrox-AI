using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

[Serializable]
public class FMODCustomEmitterPacket : Packet
{
    public NitroxId Id { get; }
    public string AssetPath { get; }
    public bool Play { get; }

    /// <summary>
    ///     The event loops until stopped, so the server keeps it for players who arrive or join while it plays.
    /// </summary>
    public bool Looping { get; }

    public FMODCustomEmitterPacket(NitroxId id, string assetPath, bool play, bool looping)
    {
        Id = id;
        AssetPath = assetPath;
        Play = play;
        Looping = looping;
    }
}
