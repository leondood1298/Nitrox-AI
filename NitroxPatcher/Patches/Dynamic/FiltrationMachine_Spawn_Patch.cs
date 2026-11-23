using System.Reflection;
using NitroxClient.GameLogic;
using UnityEngine;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A water filtration machine's new bottle of water or lump of salt is made by the player simulating the machine
///     and reported as an item of the machine (see <see cref="FiltrationMachines.LetGameSpawn" />).
/// </summary>
public sealed partial class FiltrationMachine_Spawn_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((FiltrationMachine t) => t.Spawn(default));

    public static bool Prefix(FiltrationMachine __instance, GameObject prefab, ref bool __result)
    {
        return Resolve<FiltrationMachines>().LetGameSpawn(__instance, prefab, ref __result);
    }
}
