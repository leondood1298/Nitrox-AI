using System;
using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     The current text and items of a rocket's time capsule, the answer to <see cref="PlayerTimeCapsuleContentRequest" />.
/// </summary>
[Serializable]
public class PlayerTimeCapsuleContent : Packet
{
    public NitroxId CapsuleId { get; }
    public PlayerTimeCapsuleMetadata Metadata { get; }
    public List<Entity> Items { get; }

    public PlayerTimeCapsuleContent(NitroxId capsuleId, PlayerTimeCapsuleMetadata metadata, List<Entity> items)
    {
        CapsuleId = capsuleId;
        Metadata = metadata;
        Items = items;
    }
}
