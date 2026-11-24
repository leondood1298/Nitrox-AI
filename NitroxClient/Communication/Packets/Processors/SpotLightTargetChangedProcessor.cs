using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class SpotLightTargetChangedProcessor : IClientPacketProcessor<SpotLightTargetChanged>
{
    public Task Process(ClientProcessorContext context, SpotLightTargetChanged packet)
    {
        // A spotlight out of range is told again when its target next changes
        if (NitroxEntity.TryGetComponentFrom(packet.SpotLightId, out BaseSpotLight spotLight))
        {
            BaseSpotLights.ApplyTarget(spotLight, packet.TargetId);
        }
        return Task.CompletedTask;
    }
}
