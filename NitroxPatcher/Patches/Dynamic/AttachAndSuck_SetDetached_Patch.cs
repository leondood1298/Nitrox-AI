using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A Bleeder that let go of its player is simulated as any other again (<see cref="Bleeders" />).
/// </summary>
public sealed partial class AttachAndSuck_SetDetached_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((AttachAndSuck t) => t.SetDetached());

    public static void Postfix(AttachAndSuck __instance) => Resolve<Bleeders>().Detached(__instance);
}
