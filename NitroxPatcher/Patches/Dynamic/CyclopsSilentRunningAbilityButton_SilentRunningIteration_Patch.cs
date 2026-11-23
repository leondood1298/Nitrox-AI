using System;
using System.Reflection;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Helper;
using NitroxClient.Extensions;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class CyclopsSilentRunningAbilityButton_SilentRunningIteration_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((CyclopsSilentRunningAbilityButton t) => t.SilentRunningIteration());

    public static bool Prefix(CyclopsSilentRunningAbilityButton __instance)
    {
        if (__instance.subRoot.TryGetNitroxId(out NitroxId nitroxId))
        {
            return Resolve<SimulationOwnership>().HasAnyLockType(nitroxId);
        }
        return false;
    }
}
