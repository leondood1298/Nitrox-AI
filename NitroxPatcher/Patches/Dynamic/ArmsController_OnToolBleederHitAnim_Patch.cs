using System.Reflection;
using NitroxClient.GameLogic.PlayerLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The Bleeder hit animation event of another player's body would make the local player's tool hit a Bleeder.
/// </summary>
public sealed partial class ArmsController_OnToolBleederHitAnim_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((ArmsController t) => t.OnToolBleederHitAnim(default));

    public static bool Prefix(ArmsController __instance) => !RemoteArms.IsRemote(__instance);
}
