using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class BaseNuclearReactor_OnHover_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((BaseNuclearReactor t) => t.OnHover());

    public static void Postfix(BaseNuclearReactor __instance) => ReactorFuelDisplay.ShowHandText(__instance);
}
