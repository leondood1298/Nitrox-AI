using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Every change of a creature's infection ends here (RemoveInfection, Heal, AlleviateSymptoms, the roll when a creature
///     appears, the relapse, containment spread and cure). Only the creature's simulating player changes it and shares it.
/// </summary>
public sealed partial class InfectedMixin_SetInfectedAmount_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((InfectedMixin t) => t.SetInfectedAmount(default));

    public static bool Prefix(InfectedMixin __instance, ref bool __result, out float __state)
    {
        __state = __instance.infectedAmount;
        // Callers such as WaterPark.InfectCreature read the result as "this creature changed"
        __result = false;
        return Resolve<CreatureInfections>().MayWrite(__instance);
    }

    public static void Postfix(InfectedMixin __instance, bool __result, float __state)
    {
        if (__result)
        {
            Resolve<CreatureInfections>().OnChanged(__instance, __state);
        }
    }
}
