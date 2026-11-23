using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     A creature of the sender's game bit something (MeleeAttack.OnTouch). The other games show the bite, its pose and
///     sound, on their copy. What the bite does is decided by the sender's game alone.
/// </summary>
[Serializable]
public class CreatureBitePlayed : Packet
{
    public NitroxId CreatureId { get; }

    public CreatureBitePlayed(NitroxId creatureId)
    {
        CreatureId = creatureId;
    }
}
