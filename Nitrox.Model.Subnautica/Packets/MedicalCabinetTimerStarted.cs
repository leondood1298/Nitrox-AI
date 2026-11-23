using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     A game runs a medical cabinet on the timer it started when it loaded the cabinet (MedicalCabinet.Start). The first
///     such state the server gets for a cabinet it has none for becomes the cabinet's; a game whose state comes later is
///     given the stored one.
/// </summary>
[Serializable]
public class MedicalCabinetTimerStarted : Packet
{
    public NitroxId Id { get; }
    public MedicalCabinetMetadata State { get; }

    public MedicalCabinetTimerStarted(NitroxId id, MedicalCabinetMetadata state)
    {
        Id = id;
        State = state;
    }
}
