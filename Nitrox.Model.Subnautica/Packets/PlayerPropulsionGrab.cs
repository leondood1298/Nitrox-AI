using System;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     A player's handheld propulsion cannon took hold of an object, or let go of it (<see cref="GrabbedId" /> null). Only
///     the beam and the glow around the object follow from it: the object itself is moved by the player's game.
/// </summary>
[Serializable]
public class PlayerPropulsionGrab : Packet
{
    public SessionId SessionId { get; }
    public NitroxId? GrabbedId { get; }

    public PlayerPropulsionGrab(SessionId sessionId, NitroxId? grabbedId)
    {
        SessionId = sessionId;
        GrabbedId = grabbedId;
    }

    public override string ToString() => $"[{nameof(PlayerPropulsionGrab)} SessionId: {SessionId}, GrabbedId: {GrabbedId}]";
}
