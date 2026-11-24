using System;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Networking;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

[Serializable]
public class PlayerInCyclopsMovement : Packet
{
    public SessionId SessionId { get; }
    public NitroxVector3 LocalPosition { get; }
    public NitroxQuaternion LocalRotation { get; }

    /// <summary>
    ///     Where the player looks, relative to the Cyclops: the body only turns, so its rotation carries no pitch.
    /// </summary>
    public NitroxQuaternion LocalAimingRotation { get; }

    public PlayerInCyclopsMovement(SessionId sessionId, NitroxVector3 localPosition, NitroxQuaternion localRotation, NitroxQuaternion localAimingRotation)
    {
        SessionId = sessionId;
        LocalPosition = localPosition;
        LocalRotation = localRotation;
        LocalAimingRotation = localAimingRotation;
        DeliveryMethod = NitroxDeliveryMethod.DeliveryMethod.UNRELIABLE_SEQUENCED;
        UdpChannel = UdpChannelId.MOVEMENTS;
    }
}
