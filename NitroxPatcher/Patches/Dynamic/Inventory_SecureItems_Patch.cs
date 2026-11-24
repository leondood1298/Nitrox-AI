using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     Entering a base or vehicle secures what the player carries; the server keeps it (see <see cref="Items.InventorySecured" />).
/// </summary>
public sealed partial class Inventory_SecureItems_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Inventory t) => t.SecureItems(default(bool)));

    public static void Postfix()
    {
        Resolve<Items>().InventorySecured();
    }
}
