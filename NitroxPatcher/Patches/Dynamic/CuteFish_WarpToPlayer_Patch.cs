using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Only the Cuddlefish's simulating game warps it, and only to its leader (<see cref="CuteFishes" />).
/// </summary>
public sealed partial class CuteFish_WarpToPlayer_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((CuteFish t) => t.WarpToPlayer());

    public static bool Prefix(CuteFish __instance) => Resolve<CuteFishes>().MayWarpToLocalPlayer(__instance);
}
