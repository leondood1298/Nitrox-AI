using System.Reflection;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class Charger_OnEquip_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Charger t) => t.OnEquip((string)null, (InventoryItem)null));

    public static void Postfix(Charger __instance)
    {
        if (!__instance.opened)
        {
            __instance.opened = true;
            __instance.animator.SetBool(__instance.animParamOpen, value: true);
            __instance.ToggleUI(active: true);
        }
    }
}
