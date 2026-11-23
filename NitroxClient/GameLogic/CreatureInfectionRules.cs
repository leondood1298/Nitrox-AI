using System;
using Nitrox.Model.DataStructures;

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
        // GetHashCode is not guaranteed to be the same across runtimes and processes
        byte[] bytes = new Guid(creatureId.ToString()).ToByteArray();
        unchecked
        {
            uint hash = 2166136261;
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
