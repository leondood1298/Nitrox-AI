using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A Lava Larva attached in this game whose simulation went to another player lets go here (see <see cref="LavaLarvae" />).
/// </summary>
public sealed partial class AttachToVehicle_Update_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((AttachToVehicle t) => t.Update());

    public static void Prefix(AttachToVehicle __instance)
    {
        Resolve<LavaLarvae>().ReleaseIfSimulatedElsewhere(__instance);
    }
}
