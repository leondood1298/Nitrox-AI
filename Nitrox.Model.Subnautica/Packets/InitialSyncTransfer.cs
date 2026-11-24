using System;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     Sent right before a large <see cref="InitialPlayerSync" />: how many bytes it has, so that the joining game can show
///     how far it has come instead of a loading screen that seems stuck.
/// </summary>
[Serializable]
public sealed class InitialSyncTransfer : Packet
{
    /// <summary>A snapshot smaller than this arrives in a moment and is not announced.</summary>
    public const int ANNOUNCED_FROM_BYTES = 64 * 1024;

    public int Bytes { get; }

    public InitialSyncTransfer(int bytes)
    {
        Bytes = bytes;
    }
}
