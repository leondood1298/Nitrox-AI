using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     The server did not keep a picture a player put into a frame. The other players never see it.
/// </summary>
[Serializable]
public sealed class PictureFrameRefused : Packet
{
    public NitroxId FrameId { get; }
    public PictureFrameRefusal Reason { get; }

    /// <summary>
    ///     The limit that was exceeded, in bytes: of one picture, or of what is left of the world's room for pictures.
    /// </summary>
    public long Limit { get; }

    public PictureFrameRefused(NitroxId frameId, PictureFrameRefusal reason, long limit)
    {
        FrameId = frameId;
        Reason = reason;
        Limit = limit;
    }
}

public enum PictureFrameRefusal
{
    /// <summary>The frame is not known to the server, or not a picture frame.</summary>
    NO_FRAME,

    /// <summary>The picture is larger than one picture may be.</summary>
    TOO_LARGE,

    /// <summary>The world holds as many pictures as it may.</summary>
    WORLD_FULL
}
