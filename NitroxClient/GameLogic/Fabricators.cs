using System.Collections.ObjectModel;
using System.Globalization;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using NitroxClient.GameLogic.Simulation;
using NitroxClient.MonoBehaviours;

namespace NitroxClient.GameLogic;

/// <summary>
///     A fabricator (also the workbench and the fabricators of the Lifepod, a Cyclops and a Scanner Room) runs one craft at
///     a time for everybody. A craft starts only once this game holds the fabricator's exclusive lock, which it asks for
///     when its player opens the fabricator's menu and keeps until the craft is done. The result is its crafter's for a
///     few seconds and then the first taker's; the server decides (see <see cref="ItemSources" />).
/// </summary>
public class Fabricators
{
    private readonly SimulationOwnership simulationOwnership;
    private readonly ItemSources itemSources;

    /// <summary>
    ///     Set while a craft held back until the lock arrived is started, so that it is not held back again.
    /// </summary>
    private bool startingHeldBackCraft;

    public Fabricators(SimulationOwnership simulationOwnership, ItemSources itemSources)
    {
        this.simulationOwnership = simulationOwnership;
        this.itemSources = itemSources;
    }

    /// <summary>
    ///     Called after the fabricator's menu was opened or closed. Opening asks for the lock early, so that the craft the
    ///     player then picks starts at once; closing without a craft gives it back.
    /// </summary>
    public void MenuOpenedChanged(GhostCrafter crafter, bool opened)
    {
        if (!crafter._logic || !crafter._logic.TryGetNitroxId(out NitroxId id))
        {
            return;
        }
        if (opened)
        {
            if (!simulationOwnership.HasExclusiveLock(id))
            {
                simulationOwnership.RequestSimulationLock(id, SimulationLockType.EXCLUSIVE);
            }
        }
        else if (simulationOwnership.HasExclusiveLock(id) && !crafter._logic.inProgress && !crafter.HasCraftedItem())
        {
            simulationOwnership.RequestSimulationLock(id, SimulationLockType.TRANSIENT);
        }
    }

    /// <summary>
    ///     Called before GhostCrafter.Craft, which takes the ingredients and starts the craft.
    /// </summary>
    /// <returns>True if the craft may start now; otherwise it starts once the lock has arrived, or not at all.</returns>
    public bool MayCraft(GhostCrafter crafter, TechType techType, float duration)
    {
        if (startingHeldBackCraft || !crafter._logic || !crafter._logic.TryGetNitroxId(out NitroxId id) || simulationOwnership.HasExclusiveLock(id))
        {
            return true;
        }

        LockRequest<HeldBackCraft> request = new(id, SimulationLockType.EXCLUSIVE, StartHeldBackCraft, new HeldBackCraft(crafter, techType, duration));
        simulationOwnership.RequestSimulationLock(request);
        return false;
    }

    /// <summary>
    ///     Called before CrafterLogic.TryPickup, which makes the result and gives it to the player. Every game that takes
    ///     the result makes its own, so the result is announced with what was crafted when.
    /// </summary>
    public void TakingResult(CrafterLogic crafterLogic)
    {
        if (crafterLogic.pickingUp || crafterLogic.craftingTechType == TechType.None || !crafterLogic.TryGetNitroxId(out NitroxId id))
        {
            return;
        }

        string craft = $"craft{crafterLogic.timeCraftingBegin.ToString("R", CultureInfo.InvariantCulture)}";
        ReadOnlyCollection<TechType> linkedItems = TechData.GetLinkedItems(crafterLogic.craftingTechType);
        TechType current = crafterLogic.linkedIndex == -1 || linkedItems == null || crafterLogic.linkedIndex >= linkedItems.Count
            ? crafterLogic.craftingTechType
            : linkedItems[crafterLogic.linkedIndex];

        // Named by how many were left when it was taken, the same in every game
        for (int left = crafterLogic.numCrafted; left > 0; left--)
        {
            itemSources.Expect(current, id, $"{craft}:{crafterLogic.linkedIndex}:{left}");
        }
        if (linkedItems != null)
        {
            for (int index = crafterLogic.linkedIndex + 1; index < linkedItems.Count; index++)
            {
                itemSources.Expect(linkedItems[index], id, $"{craft}:{index}:1");
            }
        }
    }

    private void StartHeldBackCraft(NitroxId id, bool lockAcquired, HeldBackCraft context)
    {
        GhostCrafter crafter = context.Crafter;
        if (!crafter)
        {
            return;
        }
        if (!lockAcquired)
        {
            ErrorMessage.AddMessage(Language.main.Get("Nitrox_FabricatorBusy"));
            return;
        }
        if (!crafter._logic || crafter._logic.inProgress || crafter.HasCraftedItem())
        {
            return;
        }

        startingHeldBackCraft = true;
        try
        {
            crafter.Craft(context.TechType, context.Duration);
        }
        finally
        {
            startingHeldBackCraft = false;
        }
    }

    private sealed class HeldBackCraft(GhostCrafter crafter, TechType techType, float duration) : LockRequestContext
    {
        public GhostCrafter Crafter { get; } = crafter;
        public TechType TechType { get; } = techType;
        public float Duration { get; } = duration;
    }
}
