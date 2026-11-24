using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     A 2D (non-positional) event of an emitter on an entity: the receiver fades it by its distance to the entity.
/// </summary>
[Serializable]
public class FMODEventInstancePacket : FMODAssetPacket
{
    public NitroxId Id { get; }
    public bool Play { get; }

    /// <inheritdoc cref="FMODCustomEmitterPacket.Looping" />
    public bool Looping { get; }

    public FMODEventInstancePacket(NitroxId id, bool play, string assetPath, NitroxVector3 position, float volume, bool looping) : base(assetPath, position, volume)
    {
        Id = id;
        Play = play;
        Looping = looping;
    }
}
