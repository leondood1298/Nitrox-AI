using System;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     A player marked a spot for a few seconds. It carries what was marked, not a finished sentence, so that each player
///     reads it in their own language.
/// </summary>
[Serializable]
public class PlayerPingCreated(SessionId sessionId, string targetTechType, string targetName, NitroxVector3 position, NitroxId pingId)
    : Packet
{
    /// <summary>
    ///     Set by the server to the sender's own session.
    /// </summary>
    public SessionId SessionId { get; } = sessionId;

    /// <summary>
    ///     The tech type of what was pinged, empty if it has none.
    /// </summary>
    public string TargetTechType { get; } = targetTechType;

    /// <summary>
    ///     What was pinged as the sender's game names it, for a target without tech type (terrain, a wreck part).
    /// </summary>
    public string TargetName { get; } = targetName;

    public NitroxVector3 Position { get; } = position;
    public NitroxId PingId { get; } = pingId;
}
