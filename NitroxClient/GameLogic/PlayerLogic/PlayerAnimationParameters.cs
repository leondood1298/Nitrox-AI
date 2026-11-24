using System.Collections.Generic;
using Nitrox.Model.GameLogic.PlayerAnimation;

namespace NitroxClient.GameLogic.PlayerLogic;

/// <summary>
///     The parameters of the game's player animator that are copied to how other players see this player. Movement, the
///     held item and being underwater are synced by other means.
/// </summary>
public static class PlayerAnimationParameters
{
    /// <summary>
    ///     On/off parameters. The game sets them on the local player every frame (<c>ArmsController.Update</c>,
    ///     <c>ArmsController.SetUsingPda</c> and the tools themselves), so other players' copies only have to follow.
    /// </summary>
    public static IReadOnlyDictionary<AnimChangeType, string> Switches { get; } = new Dictionary<AnimChangeType, string>
    {
        [AnimChangeType.USING_PDA] = "using_pda",
        [AnimChangeType.USING_TOOL] = "using_tool",
        [AnimChangeType.USING_TOOL_ALT] = "using_tool_alt",
        [AnimChangeType.JUMPING] = "jump",
        [AnimChangeType.BASHING] = "bash",
        [AnimChangeType.DIVING] = "diving",
        [AnimChangeType.DIVING_LAND] = "diving_land",
        // ArmsController.Update
        [AnimChangeType.GRABBED] = "grab",
        [AnimChangeType.BLEEDER_ATTACHED] = "bleeder",
        // The ConditionRules of ArmsController.InstallAnimationRules, which are switched off on other players' bodies
        [AnimChangeType.ON_SURFACE] = "on_surface",
        // StasisRifle.Charge
        [AnimChangeType.STASIS_RIFLE_CHARGED] = "charged_stasisrifle",
        // DiveReel.SetDiveReelOutOfAmmoAnimator
        [AnimChangeType.DIVE_REEL_OUT_OF_AMMO] = "divereel_outofammo",
        // Exosuit.SlotLeftDown/SlotRightDown and their Up
        [AnimChangeType.EXOSUIT_USE_LEFT] = "exosuit_use_left",
        [AnimChangeType.EXOSUIT_USE_RIGHT] = "exosuit_use_right"
    };

    /// <summary>
    ///     Parameters that start an animation once. Set by <c>Player.OnKill</c> and <c>DiveReel.UpdateEquipped</c>, and
    ///     sent by patches on these.
    /// </summary>
    public static IReadOnlyDictionary<AnimChangeType, string> Triggers { get; } = new Dictionary<AnimChangeType, string>
    {
        [AnimChangeType.DEATH] = "player_death",
        [AnimChangeType.DEATH_FIRE] = "player_death_fire",
        [AnimChangeType.DEATH_EXPLOSION] = "player_death_explosion",
        [AnimChangeType.DIVE_REEL_RESET] = "divereel_reset"
    };
}
