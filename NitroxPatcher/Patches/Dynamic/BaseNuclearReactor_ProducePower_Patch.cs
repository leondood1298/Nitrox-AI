using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class BaseNuclearReactor_ProducePower_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((BaseNuclearReactor t) => t.ProducePower(0f));

    public static bool Prefix(BaseNuclearReactor __instance)
    {
        return !__instance.TryGetNitroxId(out var id) || Resolve<SimulationOwnership>().HasAnyLockType(id);
    }
}
