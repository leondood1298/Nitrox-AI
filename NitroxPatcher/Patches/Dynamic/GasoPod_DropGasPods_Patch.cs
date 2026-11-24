using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The pods a Gasopod releases are made from a seed every game shares (<see cref="CreatureEffects" />).
/// </summary>
public sealed partial class GasoPod_DropGasPods_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((GasoPod t) => t.DropGasPods());

    public static void Prefix(GasoPod __instance, out Random.State __state)
    {
        __state = Resolve<CreatureEffects>().BeginGasRelease(__instance);
    }

    public static void Postfix(Random.State __state)
    {
        Resolve<CreatureEffects>().EndGasRelease(__state);
    }
}
