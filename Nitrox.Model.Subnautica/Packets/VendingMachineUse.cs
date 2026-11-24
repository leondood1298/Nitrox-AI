using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

/// <summary>
///     A player took a snack from a vending machine: the machine waits its interval for everybody, as the coffee machine
///     does. Its sound reaches the others through the sound relay.
/// </summary>
[Serializable]
public sealed class VendingMachineUse : Packet
{
    public NitroxId Id { get; }

    public VendingMachineUse(NitroxId id)
    {
        Id = id;
    }
}
