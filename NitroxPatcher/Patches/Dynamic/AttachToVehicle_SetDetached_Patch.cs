using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A Lava Larva came off a vehicle in the game that had it attached; the others show it (see <see cref="LavaLarvae" />).
/// </summary>
public sealed partial class AttachToVehicle_SetDetached_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((AttachToVehicle t) => t.SetDetached());

    public static void Prefix(AttachToVehicle __instance)
    {
        Resolve<LavaLarvae>().Detaching(__instance);
    }
}
