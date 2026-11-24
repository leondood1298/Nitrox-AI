using System.Threading.Tasks;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class PlayerPropulsionGrabProcessor(PlayerHandAndSeatStates handAndSeatStates) : IAuthPacketProcessor<PlayerPropulsionGrab>
{
    private readonly PlayerHandAndSeatStates handAndSeatStates = handAndSeatStates;

    public async Task Process(AuthProcessorContext context, PlayerPropulsionGrab packet)
    {
        handAndSeatStates.OnPropulsionGrab(context.Sender.SessionId, packet.GrabbedId);
        await context.SendToOthersAsync(new PlayerPropulsionGrab(context.Sender.SessionId, packet.GrabbedId));
    }
}
