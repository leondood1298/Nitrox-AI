using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A craft takes its ingredients and starts only once this game holds the fabricator's exclusive lock, so that two
///     players never run two crafts on one fabricator (see <see cref="Fabricators" />).
/// </summary>
public sealed partial class GhostCrafter_Craft_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((GhostCrafter t) => t.Craft(default(TechType), default(float)));

    public static bool Prefix(GhostCrafter __instance, TechType techType, float duration)
    {
        return Resolve<Fabricators>().MayCraft(__instance, techType, duration);
    }
}
