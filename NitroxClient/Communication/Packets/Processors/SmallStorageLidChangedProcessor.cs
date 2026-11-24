using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class SmallStorageLidChangedProcessor : IClientPacketProcessor<SmallStorageLidChanged>
{
    private const string OPENED_PARAMETER = "opened";

    public Task Process(ClientProcessorContext context, SmallStorageLidChanged packet)
    {
        if (!NitroxEntity.TryGetObjectFrom(packet.Id, out GameObject gameObject))
        {
            return Task.CompletedTask;
        }
        SmallStorage smallStorage = gameObject.GetComponentInChildren<SmallStorage>(true);
        if (!smallStorage || !smallStorage.animator || smallStorage.animator.GetBool(OPENED_PARAMETER) == packet.Open)
        {
            return Task.CompletedTask;
        }
        // ToggleDoor also plays the game's lid sound
        using (PacketSuppressor<SmallStorageLidChanged>.Suppress())
        {
            smallStorage.ToggleDoor();
        }
        return Task.CompletedTask;
    }
}
