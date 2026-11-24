using System.Reflection;
using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The game destroys a home's crashfish with the home, because its fish is never saved. Here the crashfish is a stored
///     entity of its cell: a home that goes away because its cell unloads keeps it, and both are paired again when they
///     are loaded (see <see cref="CrashHomes" />). Deleting it there took the fish from every other player too and left
///     the home empty for twenty minutes.
/// </summary>
public sealed partial class CrashHome_OnDestroy_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((CrashHome t) => t.OnDestroy());

    public static bool Prefix(CrashHome __instance)
    {
        if (__instance.TryGetNitroxId(out NitroxId crashHomeId))
        {
            Resolve<CrashHomes>().Forget(crashHomeId);
        }
        return false;
    }
}
