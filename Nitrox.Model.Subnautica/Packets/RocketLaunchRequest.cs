using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     A player clicked the launch button. The server answers with <see cref="RocketLaunch" /> to everybody or with
///     <see cref="RocketLaunchRefused" /> to the asking player.
/// </summary>
[Serializable]
public class RocketLaunchRequest : Packet
{
    public NitroxId RocketId { get; }

    /// <summary>
    ///     World position of the rocket's cockpit centre ("AtmosphereVolume"). Its offset from the rocket's root is
    ///     prefab data only the client has.
    /// </summary>
    public NitroxVector3 CockpitCenter { get; }

    public RocketLaunchRequest(NitroxId rocketId, NitroxVector3 cockpitCenter)
    {
        RocketId = rocketId;
        CockpitCenter = cockpitCenter;
    }
}
