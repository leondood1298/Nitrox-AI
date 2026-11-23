using System;
using System.Collections.Generic;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     The goals that followed from the Aurora's warnings and explosion, which a game took back when the Aurora was
///     restored. Only a game knows them: the story's data names them. The server takes them back in what it stores when
///     they come from the player who restored the Aurora.
/// </summary>
[Serializable]
public class AuroraFollowUpGoalsCleared : Packet
{
    public List<string> GoalKeys { get; }

    public AuroraFollowUpGoalsCleared(List<string> goalKeys)
    {
        GoalKeys = goalKeys;
    }
}
