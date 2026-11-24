using System.Threading.Tasks;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class WelderUseProcessor(PlayerHandAndSeatStates handAndSeatStates) : IAuthPacketProcessor<WelderUse>
{
    private readonly PlayerHandAndSeatStates handAndSeatStates = handAndSeatStates;

    public async Task Process(AuthProcessorContext context, WelderUse packet)
    {
        handAndSeatStates.OnWelderUse(context.Sender.SessionId, packet.Welding);
        // The session comes from the connection, not from what the packet claims
        await context.SendToOthersAsync(new WelderUse(context.Sender.SessionId, packet.Welding));
    }
}
