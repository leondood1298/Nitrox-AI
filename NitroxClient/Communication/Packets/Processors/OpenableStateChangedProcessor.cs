using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class OpenableStateChangedProcessor : IClientPacketProcessor<OpenableStateChanged>
{
    public Task Process(ClientProcessorContext context, OpenableStateChanged packet)
    {
        // A door out of range, or of a piece no longer there when replayed after the loading screen
        if (!NitroxEntity.TryGetComponentFrom(packet.Id, out Openable openable))
        {
            return Task.CompletedTask;
        }

        using (PacketSuppressor<OpenableStateChanged>.Suppress())
        {
            openable.PlayOpenAnimation(packet.IsOpen, packet.Duration);
        }
        return Task.CompletedTask;
    }
}
