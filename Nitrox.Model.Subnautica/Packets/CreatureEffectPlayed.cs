using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     Something a creature of the sender's game did beyond moving and biting, which the other games play on their copy:
///     a Gasopod releasing its pods, a Crabsquid's EMP blast, a Mesmer starting or ending its hypnosis of a player. Only the
///     creature's simulating player sends it.
/// </summary>
[Serializable]
public class CreatureEffectPlayed : Packet
{
    public NitroxId CreatureId { get; }

    public CreatureEffect Effect { get; }

    /// <summary>
    ///     Seeds the game's random numbers while the effect plays, so that every game makes the same pods.
    /// </summary>
    public int Seed { get; }

    /// <summary>
    ///     The player the effect is on, for a hypnosis.
    /// </summary>
    public Optional<NitroxId> TargetId { get; }

    public CreatureEffectPlayed(NitroxId creatureId, CreatureEffect effect, int seed, Optional<NitroxId> targetId)
    {
        CreatureId = creatureId;
        Effect = effect;
        Seed = seed;
        TargetId = targetId;
    }

    public enum CreatureEffect
    {
        GAS_PODS_RELEASED,
        EMP_BLAST_CAST,
        HYPNOSIS_STARTED,
        HYPNOSIS_ENDED
    }
}
