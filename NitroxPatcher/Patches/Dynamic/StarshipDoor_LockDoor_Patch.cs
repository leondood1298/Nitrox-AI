using System.Reflection;
using NitroxClient.GameLogic;
using Nitrox.Model.DataStructures;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class StarshipDoor_LockDoor_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((StarshipDoor t) => t.LockDoor());

    public static void Prefix(StarshipDoor __instance, out bool __state)
    {
        __state = !__instance.doorLocked;
    }

    public static void Postfix(StarshipDoor __instance, bool __state)
    {
        // Sent after the change so that the metadata carries the locked state
        if (__state && __instance.TryGetIdOrWarn(out NitroxId id))
        {
            Resolve<Entities>().EntityMetadataChangedImmediately(__instance, id);
        }
    }
}
