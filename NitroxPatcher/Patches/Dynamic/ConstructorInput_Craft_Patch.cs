using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A vehicle bay builds for one player at a time: the build, and the taking of its resources, only start once the bay's
///     exclusive lock is granted (see <see cref="MobileVehicleBay.MayStartBuild" />).
/// </summary>
public sealed partial class ConstructorInput_Craft_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((ConstructorInput t) => t.Craft(default, default));

    public static bool Prefix(ConstructorInput __instance, TechType techType, float duration)
    {
        return Resolve<MobileVehicleBay>().MayStartBuild(__instance, techType, duration);
    }
}
