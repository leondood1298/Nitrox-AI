using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     The sand starts covering a Sand Shark of the sender's game (BurryInSand.TriggerSandCoverEffect): its burrow sound
///     and the dune it raises. Whether it then lies buried is its metadata (SandSharkMetadata).
/// </summary>
[Serializable]
public class SandSharkCoverStarted : Packet
{
    public NitroxId CreatureId { get; }

    /// <summary>
    ///     Where the shark goes under (BurryInSand.burryPosition).
    /// </summary>
    public NitroxVector3 Position { get; }

    /// <summary>
    ///     How it lies along the sea floor there (BurryInSand.floorAlign).
    /// </summary>
    public NitroxQuaternion Rotation { get; }

    public SandSharkCoverStarted(NitroxId creatureId, NitroxVector3 position, NitroxQuaternion rotation)
    {
        CreatureId = creatureId;
        Position = position;
        Rotation = rotation;
    }
}
