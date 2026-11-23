using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A Stalker that picks something up needs the item's lock first, see <see cref="StalkerShinyGrabs" />.
/// </summary>
public sealed partial class CollectShiny_TryPickupShinyTarget_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((CollectShiny t) => t.TryPickupShinyTarget());

    public static bool Prefix(CollectShiny __instance) => Resolve<StalkerShinyGrabs>().MayPickUp(__instance);
}
