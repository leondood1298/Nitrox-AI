using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Nitrox.Model.DataStructures;
using NitroxClient.GameLogic;
using NitroxClient.GameLogic.Helper;
using NitroxPatcher.PatternMatching;
using static System.Reflection.Emit.OpCodes;

namespace NitroxPatcher.Patches.Dynamic;

/// <summary>
///     A Prawn Suit's claw picks a fruit or sample into the suit's storage: the game makes a new item for it, which is
///     reported here before it goes into the storage, as <see cref="Inventory.Pickup" /> does for a player's hand. It
///     names what it was picked from, so that two suits picking the same fruit at the same moment do not both keep it
///     (see <see cref="ItemSources" />).
/// </summary>
public sealed partial class PickPrefab_AddToContainerAsync_Patch : NitroxPatch, IDynamicPatch
{
    internal static readonly MethodInfo TARGET_METHOD = AccessTools.EnumeratorMoveNext(Reflect.Method((PickPrefab t) => t.AddToContainerAsync(default, default)));

    private static readonly InstructionsPattern pattern = new()
    {
        { Reflect.Method((Pickupable t) => t.Initialize()), "Initialize" }
    };

    /*
     * component.Initialize();
     * ItemMade(pickPrefab, this.container, component);      <------- [INSERTED LINE]
     * InventoryItem item = new InventoryItem(component);
     * container.UnsafeAdd(item);
     */
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.InsertAfterMarker(pattern, "Initialize", [
            new CodeInstruction(Ldloc_1),
            new CodeInstruction(Ldarg_0),
            new CodeInstruction(Ldfld, AccessTools.Field(TARGET_METHOD.DeclaringType, "container")),
            new CodeInstruction(Ldloc_3),
            new CodeInstruction(Call, ((Action<PickPrefab, ItemsContainer, Pickupable>)ItemMade).Method)
        ]);
    }

    private static void ItemMade(PickPrefab pickPrefab, ItemsContainer container, Pickupable pickupable)
    {
        if (!InventoryContainerHelper.TryGetOwnerId(container.tr, out NitroxId ownerId))
        {
            return;
        }
        Resolve<ItemSources>().Picking(pickPrefab);
        Resolve<Items>().PickedUp(pickupable.gameObject, pickupable.GetTechType(), ownerId);
    }
}
