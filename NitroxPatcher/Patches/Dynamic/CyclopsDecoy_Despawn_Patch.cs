using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A decoy launched in this game takes its copies in the other games with it (see <see cref="CyclopsDecoys" />).
/// </summary>
public sealed partial class CyclopsDecoy_Despawn_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((CyclopsDecoy t) => t.Despawn());

    public static void Prefix(CyclopsDecoy __instance)
    {
        Resolve<CyclopsDecoys>().OnEnding(__instance);
    }
}
