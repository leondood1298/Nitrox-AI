using System;
using System.Runtime.Serialization;
using BinaryPack.Attributes;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;

namespace Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

/// <summary>
///     Whether a Cuddlefish follows a player (CuteFish.followingPlayer, which the game saves) and which player: the one who
///     last told it to follow or to stay.
/// </summary>
[Serializable, DataContract]
public class CuteFishMetadata : CreatureMetadata
{
    [DataMember(Order = 1)]
    public bool FollowingPlayer { get; }

    /// <summary>
    ///     The id of the body of the player who last told it to follow or to stay.
    /// </summary>
    [DataMember(Order = 2)]
    public Optional<NitroxId> LeaderId { get; }

    [IgnoreConstructor]
    protected CuteFishMetadata()
    {
        // Constructor for serialization. Has to be "protected" for json serialization.
    }

    public CuteFishMetadata(bool followingPlayer, Optional<NitroxId> leaderId, float infectedAmount, float enzymeEndTime = NO_ENZYME, bool hasLeash = false,
                            NitroxVector3 leashPosition = default, bool hasSize = false, float size = 0f)
        : base(infectedAmount, enzymeEndTime, hasLeash, leashPosition, hasSize, size)
    {
        FollowingPlayer = followingPlayer;
        LeaderId = leaderId;
    }

    public override string ToString()
    {
        return $"[{nameof(CuteFishMetadata)} FollowingPlayer: {FollowingPlayer}, LeaderId: {LeaderId}, {base.ToString()}]";
    }
}
