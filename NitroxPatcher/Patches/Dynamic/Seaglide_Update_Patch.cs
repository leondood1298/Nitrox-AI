using System.Reflection;
using NitroxClient.MonoBehaviours;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A seaglide in another player's hand spins its propeller while that player swims, instead of following the local
///     player's input and draining its battery here (<see cref="RemotePlayerToolLook.TryAnimateSeaglide" />).
/// </summary>
public sealed partial class Seaglide_Update_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Seaglide t) => t.Update());

    public static bool Prefix(Seaglide __instance)
    {
        return __instance.usingPlayer || !RemotePlayerToolLook.TryAnimateSeaglide(__instance);
    }
}
