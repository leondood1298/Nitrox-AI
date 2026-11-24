using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A decoy launched in this game tells the other games where it came to rest (see <see cref="CyclopsDecoys" />).
/// </summary>
public sealed partial class CyclopsDecoy_Update_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((CyclopsDecoy t) => t.Update());

    public static void Postfix(CyclopsDecoy __instance)
    {
        Resolve<CyclopsDecoys>().OnUpdated(__instance);
    }
}
