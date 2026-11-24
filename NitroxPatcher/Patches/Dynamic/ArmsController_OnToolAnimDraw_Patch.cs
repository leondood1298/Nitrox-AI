using System.Reflection;
using NitroxClient.GameLogic.PlayerLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The draw animation event of another player's body would tell the local player's tool that it was drawn.
/// </summary>
public sealed partial class ArmsController_OnToolAnimDraw_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((ArmsController t) => t.OnToolAnimDraw(default));

    public static bool Prefix(ArmsController __instance) => !RemoteArms.IsRemote(__instance);
}
