using System;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;

namespace NitroxClient.GameLogic.FMOD;

/// <summary>
///     Marks sounds this game plays because it repeats something another player did (a packet it received, or what such a
///     packet started later). The player who did it already sends the sound to everyone, so it is not sent on again, and a
///     non-positional sound is faded by the distance to where it happens instead of playing at full volume everywhere.
/// </summary>
public readonly struct ReplayedSoundScope : IDisposable
{
    private static int depth;

    public static bool IsActive => depth > 0;

    private readonly PacketSuppressor<FMODAssetPacket, FMODEventInstancePacket, FMODCustomEmitterPacket, FootstepPacket, FMODStudioEmitterPacket> suppressor;

    private ReplayedSoundScope(PacketSuppressor<FMODAssetPacket, FMODEventInstancePacket, FMODCustomEmitterPacket, FootstepPacket, FMODStudioEmitterPacket> suppressor)
    {
        this.suppressor = suppressor;
    }

    public static ReplayedSoundScope Enter()
    {
        depth++;
        return new ReplayedSoundScope(FMODSystem.SuppressSendingSounds());
    }

    public void Dispose()
    {
        if (depth > 0)
        {
            depth--;
        }
        suppressor.Dispose();
    }
}
