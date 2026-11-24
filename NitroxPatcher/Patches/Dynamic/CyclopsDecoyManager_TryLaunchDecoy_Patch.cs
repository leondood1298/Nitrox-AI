using System.Reflection;
using NitroxClient.GameLogic;
using Nitrox.Model.DataStructures;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Tells the others of a decoy launch (voice line, cooldown) only when the game launched one: an empty tube launches
///     nothing.
/// </summary>
public sealed partial class CyclopsDecoyManager_TryLaunchDecoy_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((CyclopsDecoyManager t) => t.TryLaunchDecoy());

    public static void Postfix(CyclopsDecoyManager __instance, bool __result)
    {
        if (__result && __instance.subRoot.TryGetIdOrWarn(out NitroxId id))
        {
            Resolve<Cyclops>().BroadcastLaunchDecoy(id);
        }
    }
}
