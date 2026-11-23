using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class BaseBioReactor_Update_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((BaseBioReactor t) => t.Update());

    public static void Postfix(BaseBioReactor __instance)
    {
        Resolve<BasePowerSources>().BroadcastIfSimulated(__instance, __instance._powerSource);
    }
}
