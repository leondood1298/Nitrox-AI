using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Shares the text of the rocket's time capsule as it is typed.
/// </summary>
public sealed partial class uGUI_TimeCapsuleTab_OnTextChanged_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((uGUI_TimeCapsuleTab t) => t.OnTextChanged(default));

    public static void Prefix(string text)
    {
        Resolve<TimeCapsules>().BroadcastText(text);
    }
}
