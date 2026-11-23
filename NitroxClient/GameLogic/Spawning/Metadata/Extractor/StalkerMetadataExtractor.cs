using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Extractor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Extractor;

public class StalkerMetadataExtractor : EntityMetadataExtractor<Stalker, StalkerMetadata>
{
    public override StalkerMetadata Extract(Stalker stalker)
    {
        // A Stalker raised in an Alien Containment keeps its growth in WaterParkCreatureMetadata, its one metadata slot
        if (stalker.GetComponent<WaterParkCreature>())
        {
            return null;
        }
        CreatureFriend creatureFriend = stalker.creatureFriend;
        GameObject friend = creatureFriend ? creatureFriend.GetFriend() : null;
        Optional<NitroxId> friendId = friend && friend.TryGetNitroxId(out NitroxId id) ? Optional.Of(id) : Optional.Empty;
        double friendshipEndTime = friendId.HasValue ? creatureFriend.timeFriendshipEnd : 0d;
        return new(friendId, friendshipEndTime, Resolve<CreatureInfections>().GetDecidedAmount(stalker));
    }
}
