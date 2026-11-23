using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Every player's copy of a Sea Emperor baby counted its own drops on its own timer. A baby that is an entity drops on its
///     simulating player's timer only, and an outside baby's count is shared (it may drop only so many balls).
/// </summary>
public sealed partial class DropEnzymes_Update_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((DropEnzymes t) => t.Update());

    public static bool Prefix(DropEnzymes __instance, out int __state)
    {
        __state = __instance.spawnCount;
        return Resolve<SeaEmperorBabies>().MayDrop(__instance);
    }

    public static void Postfix(DropEnzymes __instance, int __state)
    {
        if (__instance.spawnCount != __state)
        {
            Resolve<SeaEmperorBabies>().OnDropped(__instance);
        }
    }
}
