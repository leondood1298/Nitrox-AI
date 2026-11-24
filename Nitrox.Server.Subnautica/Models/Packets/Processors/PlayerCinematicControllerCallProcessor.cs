using System.Threading.Tasks;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
///     Remembers the cinematic each player is in, for players who join during it, and relays it under the sender's own
///     session.
/// </summary>
internal sealed class PlayerCinematicControllerCallProcessor(PlayerHandAndSeatStates handAndSeatStates) : IAuthPacketProcessor<PlayerCinematicControllerCall>
{
    private readonly PlayerHandAndSeatStates handAndSeatStates = handAndSeatStates;

    public async Task Process(AuthProcessorContext context, PlayerCinematicControllerCall packet)
    {
        PlayerCinematicControllerCall call = new(context.Sender.SessionId, packet.ControllerID, packet.ControllerNameHash, packet.Key, packet.StartPlaying, packet.AnimationParameters);
        handAndSeatStates.OnCinematic(context.Sender.SessionId, call);
        await context.SendToOthersAsync(call);
    }
}
