using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A Lava Larva attached itself in the game simulating it; the others show it (see <see cref="LavaLarvae" />).
/// </summary>
public sealed partial class AttachToVehicle_SetAttached_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((AttachToVehicle t) => t.SetAttached());

    public static void Postfix(AttachToVehicle __instance)
    {
        Resolve<LavaLarvae>().Attached(__instance);
    }
}
