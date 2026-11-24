using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Every game that takes a fabricator's result makes its own: the server gives it to one player (see
///     <see cref="Fabricators" />).
/// </summary>
public sealed partial class CrafterLogic_TryPickup_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((CrafterLogic t) => t.TryPickup());

    public static void Prefix(CrafterLogic __instance)
    {
        Resolve<Fabricators>().TakingResult(__instance);
    }
}
