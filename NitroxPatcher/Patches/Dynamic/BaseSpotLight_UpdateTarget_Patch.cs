using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Only the game simulating a base spotlight chooses what it follows; the others are told (see
///     <see cref="BaseSpotLights" />).
/// </summary>
public sealed partial class BaseSpotLight_UpdateTarget_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((BaseSpotLight t) => t.UpdateTarget());

    public static bool Prefix(BaseSpotLight __instance, out Creature __state)
    {
        __state = __instance.trackTarget;
        return Resolve<BaseSpotLights>().GetTargeting(__instance) != SpotLightTargeting.FOLLOW;
    }

    public static void Postfix(BaseSpotLight __instance, Creature __state)
    {
        BaseSpotLights spotLights = Resolve<BaseSpotLights>();
        if (spotLights.GetTargeting(__instance) == SpotLightTargeting.CHOOSE_AND_SHARE)
        {
            spotLights.TargetChosen(__instance, __state);
        }
    }
}
