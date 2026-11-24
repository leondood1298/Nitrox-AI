using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The tank taken from a holder is made anew by every game that takes it: the server gives it to the first player to
///     take it (see <see cref="FireExtinguisherHolders" />).
/// </summary>
public sealed partial class FireExtinguisherHolder_OnHandClick_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((FireExtinguisherHolder t) => t.OnHandClick(default(GUIHand)));

    public static void Prefix(FireExtinguisherHolder __instance)
    {
        Resolve<FireExtinguisherHolders>().Clicking(__instance);
    }
}
