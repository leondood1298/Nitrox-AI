using Nitrox.Model.GameLogic.PlayerAnimation;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

/// <summary>
/// Broadcasts and stores the animation state of a player
/// </summary>
internal sealed class AnimationChangeEventProcessor(PlayerAnimationStates animationStates) : IAuthPacketProcessor<AnimationChangeEvent>
{
    public async Task Process(AuthProcessorContext context, AnimationChangeEvent packet)
    {
        PlayerAnimation animation = packet.Animation;
        if (animation.Type == AnimChangeType.UNDERWATER)
        {
            // Joining players are given this one animation with the player's details and use it to decide whether the
            // player is swimming. Storing any other animation here would make them get that wrong.
            context.Sender.PlayerContext.Animation = animation;
        }
        animationStates.Set(context.Sender.SessionId, animation);
        // The session comes from the connection, not from what the packet claims.
        await context.SendToOthersAsync(new AnimationChangeEvent(context.Sender.SessionId, animation));
    }
}
