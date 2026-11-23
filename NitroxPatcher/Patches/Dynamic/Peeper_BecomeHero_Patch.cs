using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Every way a peeper becomes an enzyme-carrying hero ends here: born a hero (Peeper.InitializeOnce), loaded
///     (OnProtoDeserialize), an enzyme cloud (EnzymeCloud.OnTriggerEnter) and a Precursor vent (PrecursorSurfaceVent.RetrievePeeper).
///     Each game used to decide this on its own. Only the peeper's simulating player makes it a hero now and shares it.
/// </summary>
public sealed partial class Peeper_BecomeHero_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Peeper t) => t.BecomeHero());

    public static bool Prefix(Peeper __instance, out bool __state)
    {
        __state = Resolve<CreatureInfections>().MayBecomeHero(__instance);
        return __state;
    }

    public static void Postfix(Peeper __instance, bool __state)
    {
        if (__state)
        {
            Resolve<CreatureInfections>().OnBecameHero(__instance);
        }
    }
}
