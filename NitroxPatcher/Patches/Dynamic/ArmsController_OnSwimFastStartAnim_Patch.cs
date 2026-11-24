using System.Reflection;
using NitroxClient.GameLogic.PlayerLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The fast swim stroke of another player's body would start the local player's fast swimming.
/// </summary>
public sealed partial class ArmsController_OnSwimFastStartAnim_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((ArmsController t) => t.OnSwimFastStartAnim(default));

    public static bool Prefix(ArmsController __instance) => !RemoteArms.IsRemote(__instance);
}
