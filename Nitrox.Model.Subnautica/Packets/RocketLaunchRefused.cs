using System;
using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     The server's answer to a <see cref="RocketLaunchRequest" /> when not every player in the world is aboard.
/// </summary>
[Serializable]
public class RocketLaunchRefused : Packet
{
    public NitroxId RocketId { get; }
    public List<string> PlayersNotAboard { get; }

    public RocketLaunchRefused(NitroxId rocketId, List<string> playersNotAboard)
    {
        RocketId = rocketId;
        PlayersNotAboard = playersNotAboard;
    }
}
