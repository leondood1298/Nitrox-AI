using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class BaseNuclearReactor_Update_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((BaseNuclearReactor t) => t.Update());

    public static void Postfix(BaseNuclearReactor __instance)
    {
        Resolve<BasePowerSources>().BroadcastIfSimulated(__instance, __instance._powerSource);
    }
}
