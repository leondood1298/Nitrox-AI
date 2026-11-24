using System;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     The JPEG bytes of a picture, for a player who asked for them.
/// </summary>
[Serializable]
public sealed class PictureFrameImage : Packet
{
    public string PictureHash { get; }
    public byte[] Picture { get; }

    public PictureFrameImage(string pictureHash, byte[] picture)
    {
        PictureHash = pictureHash;
        Picture = picture;
    }
}
