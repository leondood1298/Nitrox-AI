using System;
using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Networking;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     Where the entities a player simulates are. Sent ten times a second, so one lost is replaced by the next: it goes
///     unreliable on the movement channel, in pieces of at most <see cref="MAX_UPDATES_PER_PACKET" /> that each fit one
///     datagram (<see cref="Split" />). Sent reliable and ordered, as before, a lost piece held up every other packet to its
///     player until resent.
/// </summary>
[Serializable]
public class EntityTransformUpdates : Packet
{
    /// <summary>
    ///     The most updates that fit in <see cref="NitroxDeliveryMethod.MAX_UNRELIABLE_PACKET_BYTES" />, counting the
    ///     larger kind (<see cref="SplineTransformUpdate" />).
    /// </summary>
    public const int MAX_UPDATES_PER_PACKET = 12;

    public List<EntityTransformUpdate> Updates { get; }

    public EntityTransformUpdates(List<EntityTransformUpdate> updates)
    {
        Updates = updates;
        DeliveryMethod = NitroxDeliveryMethod.DeliveryMethod.UNRELIABLE_SEQUENCED;
        UdpChannel = UdpChannelId.MOVEMENTS;
    }

    /// <summary>
    ///     The updates as packets that each fit one datagram.
    /// </summary>
    public static IEnumerable<EntityTransformUpdates> Split(List<EntityTransformUpdate> updates)
    {
        for (int offset = 0; offset < updates.Count; offset += MAX_UPDATES_PER_PACKET)
        {
            yield return new EntityTransformUpdates(updates.GetRange(offset, Math.Min(MAX_UPDATES_PER_PACKET, updates.Count - offset)));
        }
    }

    public override string ToString()
    {
        return $"[{nameof(EntityTransformUpdates)} [{string.Join(" ", Updates)}]]";
    }

    [Serializable]
    public abstract class EntityTransformUpdate
    {
        public NitroxId Id { get; }
        public NitroxVector3 Position { get; set; }
        public NitroxQuaternion Rotation { get; set; }

        public EntityTransformUpdate(NitroxId id, NitroxVector3 position, NitroxQuaternion rotation)
        {
            Id = id;
            Position = position;
            Rotation = rotation;
        }
    }

    [Serializable]
    public class RawTransformUpdate : EntityTransformUpdate
    {
        public RawTransformUpdate(NitroxId id, NitroxVector3 position, NitroxQuaternion rotation) : base(id, position, rotation)
        {

        }

        public override string ToString()
        {
            return $"[{nameof(RawTransformUpdate)} Id: {Id}, Position: {Position}, Rotation: {Rotation}]";
        }
    }

    [Serializable]
    public class SplineTransformUpdate : EntityTransformUpdate
    {
        public NitroxVector3 DestinationPosition { get; }
        public NitroxVector3 DestinationDirection { get; }
        public float Velocity { get; }

        public SplineTransformUpdate(NitroxId id, NitroxVector3 position, NitroxQuaternion rotation, NitroxVector3 destinationPosition, NitroxVector3 destinationDirection, float velocity) : base(id, position, rotation)
        {
            DestinationPosition = destinationPosition;
            DestinationDirection = destinationDirection;
            Velocity = velocity;
        }

        public override string ToString()
        {
            return $"[{nameof(SplineTransformUpdate)} Id: {Id}, Position: {Position}, Rotation: {Rotation}, DestinationPosition: {DestinationPosition}, DestinationDirection: {DestinationDirection}, Velocity: {Velocity}]";
        }
    }
}
