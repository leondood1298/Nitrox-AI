using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     With several rockets the game's single time capsule shows one rocket's capsule at a time. A player who opens it
///     from another rocket's cockpit asks for that capsule's current text and items (<see cref="PlayerTimeCapsuleContent" />).
/// </summary>
[Serializable]
public class PlayerTimeCapsuleContentRequest : Packet
{
    public NitroxId CapsuleId { get; }

    public PlayerTimeCapsuleContentRequest(NitroxId capsuleId)
    {
        CapsuleId = capsuleId;
    }
}
