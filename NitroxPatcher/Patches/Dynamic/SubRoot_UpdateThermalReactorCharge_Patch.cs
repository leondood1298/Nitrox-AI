using System;
using System.Reflection;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Helper;
using NitroxClient.Extensions;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class SubRoot_UpdateThermalReactorCharge_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((SubRoot t) => t.UpdateThermalReactorCharge());

    public static bool Prefix(SubRoot __instance)
    {
        if (__instance.TryGetNitroxId(out NitroxId nitroxId) && !Resolve<SimulationOwnership>().HasAnyLockType(nitroxId))
        {
            return false;
        }
        return true;
    }
}
