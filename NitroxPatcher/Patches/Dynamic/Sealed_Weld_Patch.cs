using System.Reflection;
using NitroxClient.GameLogic;
using Nitrox.Model.DataStructures;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class Sealed_Weld_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Sealed t) => t.Weld(default(float)));

    public static void Prefix(Sealed __instance, out bool __state)
    {
        __state = __instance._sealed;
    }

    public static void Postfix(Sealed __instance, bool __state)
    {
        // Weld runs every frame while a laser cutter is used, and does nothing once the object is open
        if (!__state || !__instance.TryGetIdOrWarn(out NitroxId id))
        {
            return;
        }

        if (__instance._sealed)
        {
            Resolve<Entities>().EntityMetadataChangedThrottled(__instance, id);
        }
        else
        {
            // Opened: no throttled progress update may arrive after it
            Resolve<Entities>().EntityMetadataChangedImmediately(__instance, id);
        }
    }
}
