using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The sand starts covering a Sand Shark, see <see cref="SandSharkBurrows" />.
/// </summary>
public sealed partial class BurryInSand_TriggerSandCoverEffect_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((BurryInSand t) => t.TriggerSandCoverEffect());

    public static void Postfix(BurryInSand __instance) => Resolve<SandSharkBurrows>().CoverStarted(__instance);
}
