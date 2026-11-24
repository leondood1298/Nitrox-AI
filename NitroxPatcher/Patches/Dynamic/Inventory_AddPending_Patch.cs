using System.Reflection;
using NitroxClient.GameLogic;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     An item given to a player whose inventory is full (a crafted item, a battery swapped out of a tool, the water of a
///     stillsuit) waits until there is room. The server is told at once that the player has it: before, it heard of it
///     only once there was room, and the item was lost if the player left first.
/// </summary>
public sealed partial class Inventory_AddPending_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((Inventory t) => t.AddPending(default(Pickupable)));

    public static void Postfix(Pickupable pickupable)
    {
        Resolve<Items>().Pending(pickupable);
    }
}
