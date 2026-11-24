using System.Reflection;
using NitroxClient.GameLogic.PlayerLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     The extinguisher animation event of another player's body would spray the local player's extinguisher.
/// </summary>
public sealed partial class ArmsController_FireExSpray_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((ArmsController t) => t.FireExSpray(default));

    public static bool Prefix(ArmsController __instance) => !RemoteArms.IsRemote(__instance);
}
