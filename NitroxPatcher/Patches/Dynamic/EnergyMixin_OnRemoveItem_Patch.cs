using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A battery or power cell taken out of a tool or vehicle goes to the player's inventory, and the other players take it
///     out of the slot (see <see cref="ItemContainers.BatteryRemoved" />).
/// </summary>
public sealed partial class EnergyMixin_OnRemoveItem_Patch : NitroxPatch, IDynamicPatch
{
    public static readonly MethodInfo TARGET_METHOD = Reflect.Method((EnergyMixin t) => t.OnRemoveItem(default(InventoryItem)));

    public static void Postfix(EnergyMixin __instance, InventoryItem item)
    {
        if (item != null)
        {
            Resolve<ItemContainers>().BatteryRemoved(__instance, item);
        }
    }
}
