using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     A player put a picture (JPEG bytes) into a picture frame. The server keeps it with the world and tells the others
///     which picture the frame shows, or refuses it with <see cref="PictureFrameRefused" />.
/// </summary>
[Serializable]
public sealed class PictureFrameUpload : Packet
{
    public NitroxId FrameId { get; }
    public byte[] Picture { get; }

    public PictureFrameUpload(NitroxId frameId, byte[] picture)
    {
        FrameId = frameId;
        Picture = picture;
    }
}
