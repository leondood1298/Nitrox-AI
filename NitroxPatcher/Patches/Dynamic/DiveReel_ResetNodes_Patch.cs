using System.Reflection;
using Nitrox.Model.GameLogic.PlayerAnimation;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A trail the player reels in is gone for everybody (see <see cref="DiveReels" />), and other players see the arm and
///     reel animation of calling it back (<c>DiveReel.UpdateEquipped</c> pulls the divereel_reset triggers right after this).
/// </summary>
public sealed partial class DiveReel_ResetNodes_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((DiveReel t) => t.ResetNodes());

    public static void Postfix(DiveReel __instance)
    {
        Resolve<DiveReels>().TrailChanged(__instance);
        if (Player.main && __instance.usingPlayer == Player.main)
        {
            Resolve<LocalPlayer>().AnimationChange(AnimChangeType.DIVE_REEL_RESET, AnimChangeState.ON);
        }
    }
}
