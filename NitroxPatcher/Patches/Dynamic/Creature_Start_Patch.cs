using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The metadata processors of leviathans and Sea Treaders run Creature.InitializeOnce while the creature is spawned,
///     before this player learns that it simulates it, so its infection roll found no lock. Rolls it once that is known, and
///     a peeper's born-hero roll (Peeper.InitializeOnce) as well.
/// </summary>
public sealed partial class Creature_Start_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Creature t) => t.Start());

    public static void Postfix(Creature __instance)
    {
        Resolve<CreatureInfections>().RollIfUndecided(__instance);
        // Creature.InitializeOnce put the leash where the creature is: the one it was given replaces it
        Resolve<CreatureLeashes>().OnStarted(__instance);
    }
}
