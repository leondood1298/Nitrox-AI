using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;
using Nitrox.Model.DataStructures;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

/// <summary>
///     The player a Stalker is friends with (CreatureFriend), which the game keeps in its save. Friendship is decided by
///     the Stalker's simulating player and has to survive that player leaving and the server restarting.
/// </summary>
[Serializable, DataContract]
public class StalkerMetadata : CreatureMetadata
{
    /// <summary>
    ///     The id of the friend player's body, empty if the Stalker has no friend.
    /// </summary>
    [DataMember(Order = 1)]
    public Optional<NitroxId> FriendId { get; }

    /// <summary>
    ///     CreatureFriend.timeFriendshipEnd, in game time (DayNightCycle.timePassed).
    /// </summary>
    [DataMember(Order = 2)]
    public double FriendshipEndTime { get; }

    [IgnoreConstructor]
    protected StalkerMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public StalkerMetadata(Optional<NitroxId> friendId, double friendshipEndTime, float infectedAmount) : base(infectedAmount)
    {
        FriendId = friendId;
        FriendshipEndTime = friendshipEndTime;
    }

    public override string ToString()
    {
        return $"[{nameof(StalkerMetadata)} FriendId: {FriendId}, FriendshipEndTime: {FriendshipEndTime}, {base.ToString()}]";
    }
}
