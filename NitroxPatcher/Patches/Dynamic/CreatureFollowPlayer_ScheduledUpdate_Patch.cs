using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A following Cuddlefish follows the player who told it to, on its simulating player's game (<see cref="CuteFishes" />).
/// </summary>
public sealed partial class CreatureFollowPlayer_ScheduledUpdate_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((CreatureFollowPlayer t) => t.ScheduledUpdate());

    public static bool Prefix(CreatureFollowPlayer __instance) => Resolve<CuteFishes>().MayFollowLocalPlayer(__instance);
}
