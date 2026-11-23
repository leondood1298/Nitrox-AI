using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     A Sand Shark of the sender's game going under the sand or coming out of it (BurryInSand). The other players' copies
///     only follow its movement, which shows neither.
/// </summary>
[Serializable]
public class SandSharkBurrowChanged : Packet
{
    public NitroxId CreatureId { get; }
    public BurrowState State { get; }

    /// <summary>
    ///     Where the shark lies buried (BurryInSand.burryPosition).
    /// </summary>
    public NitroxVector3 Position { get; }

    /// <summary>
    ///     How it lies along the sea floor there (BurryInSand.floorAlign).
    /// </summary>
    public NitroxQuaternion Rotation { get; }

    public SandSharkBurrowChanged(NitroxId creatureId, BurrowState state, NitroxVector3 position, NitroxQuaternion rotation)
    {
        CreatureId = creatureId;
        State = state;
        Position = position;
        Rotation = rotation;
    }

    public enum BurrowState
    {
        /// <summary>
        ///     The sand starts covering it (BurryInSand.TriggerSandCoverEffect).
        /// </summary>
        COVERING,

        /// <summary>
        ///     It lies still under the sand (BurryInSand.Burry).
        /// </summary>
        BURIED,

        /// <summary>
        ///     It bursts out of the sand (BurryInSand.Update digs it up).
        /// </summary>
        DUG_UP
    }
}
