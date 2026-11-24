using System;
using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Networking;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

[Serializable]
public class VehicleMovements : Packet
{
    /// <summary>
    ///     The most movements that fit in <see cref="NitroxDeliveryMethod.MAX_UNRELIABLE_PACKET_BYTES" />, counting the
    ///     largest kind (<see cref="ExosuitMovementData" />). The 64 of before made a packet LiteNetLib refused to send
    ///     once more than about a dozen vehicles moved at once.
    /// </summary>
    public const int MAX_MOVEMENTS_PER_PACKET = 12;

    public List<MovementData> Data { get; }
    public double RealTime { get; set; }
    
    public VehicleMovements(List<MovementData> data, double realTime)
    {
        Data = data;
        RealTime = realTime;
        DeliveryMethod = NitroxDeliveryMethod.DeliveryMethod.UNRELIABLE_SEQUENCED;
        UdpChannel = UdpChannelId.MOVEMENTS;
    }
}

[Serializable]
public abstract record class MovementData(NitroxId Id, NitroxVector3 Position, NitroxQuaternion Rotation) { }

[Serializable]
public record class SimpleMovementData(NitroxId Id, NitroxVector3 Position, NitroxQuaternion Rotation)
    : MovementData(Id, Position, Rotation) { }

[Serializable]
public record class DrivenVehicleMovementData(
    NitroxId Id, NitroxVector3 Position, NitroxQuaternion Rotation,
    sbyte SteeringWheelYaw, sbyte SteeringWheelPitch, bool ThrottleApplied)
    : MovementData(Id, Position, Rotation) { }

[Serializable]
public record class ExosuitMovementData(NitroxId Id, NitroxVector3 Position, NitroxQuaternion Rotation, 
    NitroxVector3 AimTargetLeft, NitroxVector3 AimTargetRight, sbyte SteeringWheelYaw,
    sbyte SteeringWheelPitch, bool ThrottleApplied, bool IKEnabled)
    : DrivenVehicleMovementData(Id, Position, Rotation, SteeringWheelYaw, SteeringWheelPitch, ThrottleApplied) { }
