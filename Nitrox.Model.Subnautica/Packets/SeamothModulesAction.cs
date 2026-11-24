using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     A Seamoth's pilot used an upgrade module whose effect the others see (the perimeter defense).
/// </summary>
[Serializable]
public class SeamothModulesAction : Packet
{
    public NitroxTechType TechType { get; }
    public int SlotID { get; }
    public NitroxId Id { get; }

    /// <summary>How long the module was charged (Vehicle.quickSlotCharge); it sets the defense's radius and damage.</summary>
    public float Charge { get; }

    /// <summary>The charge as a fraction of the module's maximum (Vehicle.GetSlotCharge); it picks the effect.</summary>
    public float ChargeScalar { get; }

    public SeamothModulesAction(NitroxTechType techType, int slotID, NitroxId id, float charge, float chargeScalar)
    {
        TechType = techType;
        SlotID = slotID;
        Id = id;
        Charge = charge;
        ChargeScalar = chargeScalar;
    }
}
