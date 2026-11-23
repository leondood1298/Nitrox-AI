using System.Reflection;
using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class BaseNuclearReactor_ProducePower_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((BaseNuclearReactor t) => t.ProducePower(0f));

    public static bool Prefix(BaseNuclearReactor __instance)
    {
        bool hasId = __instance.TryGetNitroxId(out NitroxId id);
        return BasePowerSources.CanSimulate(hasId, hasId && Resolve<SimulationOwnership>().HasAnyLockType(id), BasePowerSources.IsRebuildingBases());
    }
}
