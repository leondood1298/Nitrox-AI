using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Lets the player who simulates a base report how far it is flooded (see <see cref="BaseFloodBroadcaster" />).
/// </summary>
public sealed partial class BaseFloodSim_Update_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((BaseFloodSim t) => t.Update());

    public static void Postfix(BaseFloodSim __instance)
    {
        Resolve<BaseFloodBroadcaster>().OnFloodSimUpdated(__instance);
    }
}
