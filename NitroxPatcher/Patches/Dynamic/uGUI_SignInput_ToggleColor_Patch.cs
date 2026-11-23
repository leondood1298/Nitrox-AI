using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Sends a sign's or label's state as soon as its colour is changed, while the player is still editing it (see
///     <see cref="SignLabels" />).
/// </summary>
public sealed partial class uGUI_SignInput_ToggleColor_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((uGUI_SignInput t) => t.ToggleColor());

    public static void Postfix(uGUI_SignInput __instance)
    {
        Resolve<SignLabels>().BroadcastState(__instance);
    }
}
