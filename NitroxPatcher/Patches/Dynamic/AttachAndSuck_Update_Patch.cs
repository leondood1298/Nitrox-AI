using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A Bleeder's simulating game sees it touch another player's body, which nothing collides with (<see cref="Bleeders" />).
/// </summary>
public sealed partial class AttachAndSuck_Update_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((AttachAndSuck t) => t.Update());

    public static void Postfix(AttachAndSuck __instance) => Resolve<Bleeders>().CheckTouchOfOtherPlayers(__instance);
}
