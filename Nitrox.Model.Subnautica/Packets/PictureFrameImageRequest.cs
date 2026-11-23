using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     A player came close enough to a frame to see its picture and does not have it yet.
/// </summary>
[Serializable]
public sealed class PictureFrameImageRequest : Packet
{
    public NitroxId FrameId { get; }
    public string PictureHash { get; }

    public PictureFrameImageRequest(NitroxId frameId, string pictureHash)
    {
        FrameId = frameId;
        PictureHash = pictureHash;
    }
}
