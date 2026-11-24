using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class VendingMachineUseProcessor : IClientPacketProcessor<VendingMachineUse>
{
    public Task Process(ClientProcessorContext context, VendingMachineUse packet)
    {
        // A machine out of range waits on its own clock until it is loaded
        if (NitroxEntity.TryGetComponentFrom(packet.Id, out VendingMachine machine))
        {
            machine.timeLastUse = Time.time;
        }
        return Task.CompletedTask;
    }
}
