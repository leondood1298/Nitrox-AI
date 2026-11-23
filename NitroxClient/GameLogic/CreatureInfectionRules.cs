using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace NitroxClient.GameLogic;

/// <summary>
///     The parts of <see cref="CreatureInfections" /> that do not need the game.
/// </summary>
public static class CreatureInfectionRules
{
    /// <summary>
    ///     A roll that never infects: Creature.ProcessInfection infects below 0.05.
    /// </summary>
    public const float NEVER_INFECTS = 1f;

    /// <summary>
    ///     A change at least this large is sent at once. Smaller ones come from InfectedMixin.Heal, which a hero peeper calls
    ///     every physics frame, and are throttled.
    /// </summary>
    public const float IMMEDIATE_SEND_STEP = 0.1f;

    /// <summary>
    ///     Stands in for Random.value in Creature.ProcessInfection. It is fixed per creature so that every player, every load
    ///     and every new simulating player get the same answer, and a healthy creature needs nothing stored.
    /// </summary>
    /// <returns>A value from 0 (included) to 1 (excluded).</returns>
    public static float Roll(NitroxId creatureId)
    {
        return Roll(creatureId, INFECTION_SEED);
    }

    /// <summary>
    ///     Stands in for Random.value in Peeper.InitializeOnce, which makes a peeper a hero from birth below
    ///     <see cref="BORN_HERO_CHANCE" />. Fixed per peeper like <see cref="Roll(NitroxId)" />, and independent of it, as
    ///     the game rolls the two separately.
    /// </summary>
    public static float BornHeroRoll(NitroxId peeperId)
    {
        return Roll(peeperId, BORN_HERO_SEED);
    }

    /// <summary>
    ///     Peeper.InitializeOnce makes a peeper outside the Safe Shallows a hero from birth below this roll.
    /// </summary>
    public const float BORN_HERO_CHANCE = 0.05f;

    /// <summary>
    ///     The game gives a peeper that was a hero from birth its enzyme again when the world is loaded and its last enzyme ran
    ///     out at least Peeper.rechargeInterval before. Here this happens when its simulating player gets it in the world.
    /// </summary>
    /// <param name="enzymeEndTime">When its last enzyme ran out or runs out, or <see cref="CreatureMetadata.NO_ENZYME" />.</param>
    public static bool MayBornHeroRecharge(float enzymeEndTime, float now, float rechargeInterval)
    {
        if (enzymeEndTime >= CreatureMetadata.LASTING_ENZYME)
        {
            return false;
        }
        return enzymeEndTime <= CreatureMetadata.NO_ENZYME || now >= enzymeEndTime + rechargeInterval;
    }

    /// <summary>
    ///     When the enzyme a peeper has now runs out. A peeper in the prison's aquarium never loses it.
    /// </summary>
    public static float GetEnzymeEndTime(float now, float enzymeAmount, bool inPrisonAquarium)
    {
        if (enzymeAmount <= 0f)
        {
            return CreatureMetadata.NO_ENZYME;
        }
        return inPrisonAquarium ? CreatureMetadata.LASTING_ENZYME : now + enzymeAmount;
    }

    /// <summary>
    ///     The Peeper.enzymeAmount a peeper whose enzyme runs out at <paramref name="enzymeEndTime" /> has now.
    /// </summary>
    public static float GetEnzymeLeft(float enzymeEndTime, float now, float enzymeDuration)
    {
        if (enzymeEndTime >= CreatureMetadata.LASTING_ENZYME)
        {
            return enzymeDuration;
        }
        return Math.Max(0f, Math.Min(enzymeDuration, enzymeEndTime - now));
    }

    // FNV-1a's offset basis, and another one for an independent roll
    private const uint INFECTION_SEED = 2166136261;
    private const uint BORN_HERO_SEED = 0x9e3779b9;

    private static float Roll(NitroxId creatureId, uint seed)
    {
        // GetHashCode is not guaranteed to be the same across runtimes and processes
        byte[] bytes = new Guid(creatureId.ToString()).ToByteArray();
        unchecked
        {
            uint hash = seed;
            foreach (byte b in bytes)
            {
                hash = (hash ^ b) * 16777619;
            }
            // FNV-1a mixes its low bits poorly: MurmurHash3's finalizer spreads them over the whole value
            hash ^= hash >> 16;
            hash *= 0x85ebca6b;
            hash ^= hash >> 13;
            hash *= 0xc2b2ae35;
            hash ^= hash >> 16;
            return (hash >> 8) / 16777216f;
        }
    }

    /// <summary>
    ///     Only the player who simulates a creature changes its infection, other players apply what they receive.
    /// </summary>
    public static bool MayWrite(bool simulatesCreature, bool applyingReceivedValue)
    {
        return simulatesCreature || applyingReceivedValue;
    }

    public static bool IsImmediateChange(float previous, float current)
    {
        return current <= 0f || current >= 1f || Math.Abs(current - previous) >= IMMEDIATE_SEND_STEP;
    }
}
