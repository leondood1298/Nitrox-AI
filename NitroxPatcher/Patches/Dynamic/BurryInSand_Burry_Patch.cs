using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A Sand Shark lies buried, see <see cref="SandSharkBurrows" />.
/// </summary>
public sealed partial class BurryInSand_Burry_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((BurryInSand t) => t.Burry());

    public static void Postfix(BurryInSand __instance) => Resolve<SandSharkBurrows>().Buried(__instance);
}
