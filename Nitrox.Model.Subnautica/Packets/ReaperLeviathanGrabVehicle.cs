using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

[Serializable]
public class ReaperLeviathanGrabVehicle(NitroxId reaperLeviathanId, NitroxId vehicleId, ReaperLeviathan.VehicleType vehicleType, double realReleaseTime) : Packet
{
    public NitroxId ReaperLeviathanId { get; } = reaperLeviathanId;
    public NitroxId VehicleId { get; } = vehicleId;
    public ReaperLeviathan.VehicleType VehicleType { get; } = vehicleType;
    public double RealReleaseTime { get; } = realReleaseTime;
}
