using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

/// <summary>
///     The picture a picture frame shows, named by the SHA-256 of its JPEG bytes, which the server keeps with the world.
///     Only the server sets it, when it has accepted an upload.
/// </summary>
[Serializable]
[DataContract]
public class PictureFrameMetadata : EntityMetadata
{
    [DataMember(Order = 1)]
    public string PictureHash { get; }

    [IgnoreConstructor]
    protected PictureFrameMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public PictureFrameMetadata(string pictureHash)
    {
        PictureHash = pictureHash;
    }

    public override string ToString()
    {
        return $"[{nameof(PictureFrameMetadata)} PictureHash: {PictureHash}]";
    }
}
