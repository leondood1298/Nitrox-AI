using System;
using System.Reflection;
using Nitrox.Model.Helper;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class Charger_OnUnequip_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Charger t) => t.OnUnequip((string)null, (InventoryItem)null));

    public static void Postfix(Charger __instance)
    {
        if (__instance.opened && !__instance.HasChargables())
        {
            __instance.opened = false;
            __instance.sequence.Reset();
            __instance.animator.SetBool(__instance.animParamOpen, value: false);
            __instance.ToggleUI(active: false);
        }
    }
}
