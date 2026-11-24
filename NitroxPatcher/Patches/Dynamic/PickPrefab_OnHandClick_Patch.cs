using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A picked fruit or sample is made anew by every game that picks it: the server gives it to the first player to pick
///     it (see <see cref="ItemSources" />).
/// </summary>
public sealed partial class PickPrefab_OnHandClick_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((PickPrefab t) => t.OnHandClick(default(GUIHand)));

    public static void Prefix(PickPrefab __instance)
    {
        Resolve<ItemSources>().Picking(__instance);
    }
}
