using System.Reflection;
using NitroxClient.GameLogic.Bases;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Holds back deconstructing a piece of a base that another player just changed, that is out of sync, or that is a
///     room with a player inside (see <see cref="RoomOccupancy" />).
/// </summary>
public sealed partial class BaseDeconstructable_DeconstructionAllowed_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((BaseDeconstructable t) => t.DeconstructionAllowed(out Reflect.Ref<string>.Field));

    public static void Postfix(BaseDeconstructable __instance, ref bool __result, ref string reason)
    {
        Resolve<RoomOccupancy>().DeconstructionAllowed(__instance, ref __result, ref reason);
    }
}
