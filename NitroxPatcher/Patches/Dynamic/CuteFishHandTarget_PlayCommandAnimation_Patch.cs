using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Telling a Cuddlefish to follow or to stay is sent and kept, with who told it (<see cref="CuteFishes" />).
/// </summary>
public sealed partial class CuteFishHandTarget_PlayCommandAnimation_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((CuteFishHandTarget t) => t.PlayCommandAnimation());

    public static void Postfix(CuteFishHandTarget __instance, bool __result)
    {
        if (__result)
        {
            Resolve<CuteFishes>().Commanded(__instance.cuteFish);
        }
    }
}
