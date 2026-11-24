using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

[Serializable]
public class FMODStudioEmitterPacket : Packet
{
    public NitroxId Id { get; }
    public string AssetPath { get; }
    public bool Play { get; }
    public bool AllowFadeout { get; }

    /// <inheritdoc cref="FMODCustomEmitterPacket.Looping" />
    public bool Looping { get; }

    public FMODStudioEmitterPacket(NitroxId id, string assetPath, bool play, bool allowFadeout, bool looping)
    {
        Id = id;
        AssetPath = assetPath;
        Play = play;
        AllowFadeout = allowFadeout;
        Looping = looping;
    }
}
