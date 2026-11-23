using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     A Stalker ate a fish from the sender's hand on the sender's game, which does not simulate it. Sent to the player who
///     simulates the Stalker, whose Stalker eats it, is healed by a hero peeper and befriends the sender (MeleeAttack.TryEat, Stalker.OnFishEat).
/// </summary>
[Serializable]
public class StalkerFedByPlayer : Packet
{
    public NitroxId StalkerId { get; }

    /// <summary>
    ///     The id of the sender's player object.
    /// </summary>
    public NitroxId PlayerId { get; }

    /// <summary>
    ///     How much the fish feeds, after its kind (MeleeAttack.TryEat): 1, 1.5 or 3.
    /// </summary>
    public float FoodValue { get; }

    /// <summary>
    ///     The fish was a hero peeper, whose enzyme heals the Stalker's infection (MeleeAttack.TryEat).
    /// </summary>
    public bool HeroPeeper { get; }

    public StalkerFedByPlayer(NitroxId stalkerId, NitroxId playerId, float foodValue, bool heroPeeper)
    {
        StalkerId = stalkerId;
        PlayerId = playerId;
        FoodValue = foodValue;
        HeroPeeper = heroPeeper;
    }
}
