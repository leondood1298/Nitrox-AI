using System;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     A player collected a time capsule found in the world. The others get its encyclopedia entry, with the content that
///     player's game fetched, as only that player's game has it.
/// </summary>
[Serializable]
public class TimeCapsuleCollected : Packet
{
    public CollectedTimeCapsule Capsule { get; }

    public TimeCapsuleCollected(CollectedTimeCapsule capsule)
    {
        Capsule = capsule;
    }
}
