using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Opening a fabricator's menu asks for the fabricator's lock, so that the craft the player picks starts at once;
///     closing it without a craft gives the lock back (see <see cref="Fabricators" />).
/// </summary>
public sealed partial class GhostCrafter_OnOpenedChanged_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((GhostCrafter t) => t.OnOpenedChanged(default(bool)));

    public static void Postfix(GhostCrafter __instance, bool opened)
    {
        Resolve<Fabricators>().MenuOpenedChanged(__instance, opened);
    }
}
