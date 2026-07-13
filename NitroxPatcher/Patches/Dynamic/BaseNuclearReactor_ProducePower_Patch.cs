using System;
using System.CodeDom.Compiler;
using System.Reflection;
using HarmonyLib;
using Nitrox.Model.Helper;
using NitroxClient.Extensions;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

public sealed class BaseNuclearReactor_ProducePower_Patch : NitroxPatch, IDynamicPatch, INitroxPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((BaseNuclearReactor t) => t.ProducePower(0f));

    public static bool Prefix(BaseNuclearReactor __instance)
    {
        return !__instance.TryGetNitroxId(out var id) || NitroxPatch.Resolve<SimulationOwnership>().HasAnyLockType(id);
    }

    [GeneratedCode("Nitrox.Analyzers", "1.0.13.0")]
    public override void Patch(Harmony harmony)
    {
        PatchMultiple(harmony, TARGET_METHOD, new Func<BaseNuclearReactor, bool>(Prefix).Method);
    }
}
