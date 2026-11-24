using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A trail the player reels in is gone for everybody (see <see cref="DiveReels" />).
/// </summary>
public sealed partial class DiveReel_ResetNodes_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((DiveReel t) => t.ResetNodes());

    public static void Postfix(DiveReel __instance)
    {
        Resolve<DiveReels>().TrailChanged(__instance);
    }
}
