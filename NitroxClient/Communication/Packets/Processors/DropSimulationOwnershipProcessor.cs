using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Packets.Processors.Core;
using NitroxClient.GameLogic;

namespace NitroxClient.Communication.Packets.Processors;

internal sealed class DropSimulationOwnershipProcessor(SimulationOwnership simulationOwnershipManager, MapRoomCameras mapRoomCameras) : IClientPacketProcessor<DropSimulationOwnership>
{
    private readonly SimulationOwnership simulationOwnershipManager = simulationOwnershipManager;
    private readonly MapRoomCameras mapRoomCameras = mapRoomCameras;

    public Task Process(ClientProcessorContext context, DropSimulationOwnership packet)
    {
        simulationOwnershipManager.DropSimulationFrom(packet.EntityId);
        mapRoomCameras.ClearControlState(packet.EntityId);
        return Task.CompletedTask;
    }
}
