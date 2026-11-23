using System.ComponentModel;
using Nitrox.Model.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Commands.Core;
using Nitrox.Server.Subnautica.Models.GameLogic;

namespace Nitrox.Server.Subnautica.Models.Commands;

// We shouldn't let the server-side use this command because it needs some stuff to happen client-side (e.g. story goals)
[RequiresPermission(Perms.ADMIN)]
[RequiresOrigin(CommandOrigin.PLAYER)]
internal sealed class AuroraCommand(StoryManager storyManager, StoryScheduler storyScheduler) : ICommandHandler<AuroraCommand.AuroraAction>
{
    private readonly StoryManager storyManager = storyManager;
    private readonly StoryScheduler storyScheduler = storyScheduler;

    [Description("Which action to apply to Aurora")]
    public async Task Execute(ICommandContext context, AuroraAction action)
    {
        switch (action)
        {
            case AuroraAction.COUNTDOWN:
                await storyManager.BroadcastExplodeAurora(false);
                break;
            case AuroraAction.RESTORE:
                // What followed from the explosion is named in the story's data, which this player's game reports
                storyManager.ExpectAuroraFollowUps(context.OriginId);
                await storyManager.BroadcastRestoreAurora(storyScheduler);
                break;
            case AuroraAction.EXPLODE:
                await storyManager.BroadcastExplodeAurora(true);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    public enum AuroraAction
    {
        COUNTDOWN,
        RESTORE,
        EXPLODE
    }
}
