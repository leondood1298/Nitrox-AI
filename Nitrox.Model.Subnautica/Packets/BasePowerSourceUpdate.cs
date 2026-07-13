using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Model.Subnautica.Packets;

[Serializable]
public sealed class BasePowerSourceUpdate : Packet
{
    public NitroxId SourceId { get; }
    public BasePowerSourceType SourceType { get; }
    public float Power { get; }
    public float MaxPower { get; }
    public long ClientSequence { get; }
    public long Revision { get; }
    public bool IsServerResponse { get; }
    public bool Granted { get; }
    public string RejectionReason { get; }
    public float FuelConsumed { get; }

    public BasePowerSourceUpdate(NitroxId sourceId, BasePowerSourceType sourceType, float power, long clientSequence = 0, float maxPower = 0f,
        long revision = 0, bool isServerResponse = false, bool granted = false, string rejectionReason = "", float fuelConsumed = 0f)
    {
        SourceId = sourceId;
        SourceType = sourceType;
        Power = power;
        MaxPower = maxPower;
        ClientSequence = clientSequence;
        Revision = revision;
        IsServerResponse = isServerResponse;
        Granted = granted;
        RejectionReason = rejectionReason;
        FuelConsumed = fuelConsumed;
    }
}
