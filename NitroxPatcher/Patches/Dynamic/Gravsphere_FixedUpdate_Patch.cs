using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A game that no longer simulates a gravity trap lets go of what it pulled (see <see cref="GravityTraps" />).
/// </summary>
public sealed partial class Gravsphere_FixedUpdate_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Gravsphere t) => t.FixedUpdate());

    public static void Prefix(Gravsphere __instance)
    {
        Resolve<GravityTraps>().Pulling(__instance);
    }
}
