using System.Collections;
using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The spawner makes the Sea Emperor babies that appear outside after leaving the prison, and the game saves them and
///     forgets the spawner. Here every game would make its own babies every session. They are made once, by one player, as
///     entities (see <see cref="SeaEmperorBabies" />).
/// </summary>
public sealed partial class SeaEmperorBabiesSpawner_Start_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((SeaEmperorBabiesSpawner t) => t.Start());

    public static bool Prefix(SeaEmperorBabiesSpawner __instance, ref IEnumerator __result)
    {
        __result = Resolve<SeaEmperorBabies>().Spawn(__instance);
        return false;
    }
}
