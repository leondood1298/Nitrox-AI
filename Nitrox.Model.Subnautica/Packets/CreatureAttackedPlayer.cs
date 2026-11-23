using System;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     A hit that a creature landed on another player's body, sent by the player simulating the creature. A creature only
///     sees targets and attacks for its simulating player, for whom every other player is a body that takes no damage, so
///     the hit has to be passed on to the player it was meant for.
/// </summary>
[Serializable]
public class CreatureAttackedPlayer : Packet
{
    public NitroxId CreatureId { get; }

    public SessionId TargetSessionId { get; }

    /// <summary>
    ///     Damage before DamageSystem.CalculateDamage. The attacked player runs it through LiveMixin.TakeDamage, so that
    ///     their own suit and game mode apply.
    /// </summary>
    public float OriginalDamage { get; }

    public DamageType Type { get; }

    public CreatureAttackedPlayer(NitroxId creatureId, SessionId targetSessionId, float originalDamage, DamageType type)
    {
        CreatureId = creatureId;
        TargetSessionId = targetSessionId;
        OriginalDamage = originalDamage;
        Type = type;
    }
}
