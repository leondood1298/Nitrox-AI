using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class BaseBioReactor_OnHover_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((BaseBioReactor t) => t.OnHover());

    public static void Postfix(BaseBioReactor __instance) => ReactorFuelDisplay.ShowHandText(__instance);
}
