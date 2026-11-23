using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Every player's copy of a hatched Sea Emperor baby drops cure balls on its own timer, so each game had its own balls
///     in its own places. Only the baby's simulating player makes them now (see <see cref="EnzymeBalls" />).
/// </summary>
public sealed partial class DropEnzymes_SpawnCureBall_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((DropEnzymes t) => t.SpawnCureBall());

    public static bool Prefix(DropEnzymes __instance)
    {
        return Resolve<EnzymeBalls>().MaySpawn(__instance);
    }
}
