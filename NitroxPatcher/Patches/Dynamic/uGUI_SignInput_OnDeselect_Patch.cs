using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Sends a sign's or label's state when the player leaves it (see <see cref="SignLabels" />).
/// </summary>
public sealed partial class uGUI_SignInput_OnDeselect_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((uGUI_SignInput t) => t.OnDeselect());

    public static void Postfix(uGUI_SignInput __instance)
    {
        Resolve<SignLabels>().BroadcastState(__instance);
    }
}
