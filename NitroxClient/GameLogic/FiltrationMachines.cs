using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     A base's water filtration machine as this client sees it. The game runs its two timers in every player's game,
///     and each game puts water and salt of its own into the machine. Only the player simulating the machine runs them
///     here: that player makes the items, as inventory items of the machine's entity, and tells everyone the timers as
///     the machine's metadata. Everyone else only shows what they are told, and a machine nobody simulates stands still.
/// </summary>
public sealed class FiltrationMachines(IPacketSender packetSender, SimulationOwnership simulationOwnership, Items items)
{
    /// <summary>
    ///     How often the timers of a running machine are sent when nothing else about them changed. Each player's
    ///     display shows the progress in percent; the water timer moves one percent in 8.4 seconds and the salt timer in
    ///     4.2. A guess at often enough to look alive without a packet a second per machine.
    /// </summary>
    private const float PROGRESS_SEND_INTERVAL_SECONDS = 5f;

    private readonly Dictionary<NitroxId, Broadcast> lastBroadcastByMachine = [];

    /// <summary>
    ///     What this client last sent for a machine it simulates.
    /// </summary>
    /// <param name="Time">Time.realtimeSinceStartup when it was sent.</param>
    public readonly record struct Broadcast(float Time, float TimeRemainingWater, float TimeRemainingSalt);

    /// <summary>
    ///     Whether this client runs a machine's timers and makes its items.
    ///     <para>
    ///     Not while joining or while rebuilding the bases from a resync: the items the machine already holds are then
    ///     still on their way, and the machine would fill up again on top of them.
    ///     </para>
    /// </summary>
    public static bool FiltersHere(bool hasId, bool simulatedLocally, bool rebuildingBases, bool finishedLoading)
    {
        if (!hasId)
        {
            // Not a synchronized entity: it behaves as in the game
            return true;
        }
        return simulatedLocally && !rebuildingBases && finishedLoading;
    }

    /// <summary>
    ///     Decides whether the timers of a machine this client simulates are sent now.
    /// </summary>
    /// <param name="last">What was sent last for this machine, or null when nothing was.</param>
    public static bool ShouldBroadcast(Broadcast? last, float now, float timeRemainingWater, float timeRemainingSalt)
    {
        if (last is not { } previous)
        {
            return true;
        }
        if (previous.TimeRemainingWater == timeRemainingWater && previous.TimeRemainingSalt == timeRemainingSalt)
        {
            // Idle, full or without power: there is nothing new
            return false;
        }
        // A timer that starts or stops changes what the display says and whether the machine works, and one that
        // started over has made an item; neither waits
        if (IsRunning(previous.TimeRemainingWater) != IsRunning(timeRemainingWater) || IsRunning(previous.TimeRemainingSalt) != IsRunning(timeRemainingSalt) ||
            timeRemainingWater > previous.TimeRemainingWater || timeRemainingSalt > previous.TimeRemainingSalt)
        {
            return true;
        }
        return now - previous.Time >= PROGRESS_SEND_INTERVAL_SECONDS;
    }

    /// <summary>
    ///     A stopped timer is <see cref="FiltrationMachineMetadata.IDLE" />; the game counts a running one down to zero
    ///     and stops it in the same update.
    /// </summary>
    private static bool IsRunning(float timeRemaining) => timeRemaining > 0f;

    public static FiltrationMachineMetadata GetState(FiltrationMachine machine) => new(machine.timeRemainingWater, machine.timeRemainingSalt);

    private static bool IsLoaded() => Multiplayer.Main && Multiplayer.Main.InitialSyncCompleted;

    /// <inheritdoc cref="FiltersHere(bool, bool, bool, bool)" />
    /// <remarks>
    ///     A machine that does not filter here still has its model refreshed, as the game's update would have: the
    ///     model shows whether it works and the item it holds.
    /// </remarks>
    public bool FiltersHere(FiltrationMachine machine)
    {
        bool hasId = machine.TryGetNitroxId(out NitroxId machineId);
        if (FiltersHere(hasId, hasId && simulationOwnership.HasAnyLockType(machineId), BasePowerSources.IsRebuildingBases(), IsLoaded()))
        {
            return true;
        }
        // Taking the machine over later then sends its state at once
        lastBroadcastByMachine.Remove(machineId);
        BaseFiltrationMachineGeometry model = machine.GetModel();
        if (model)
        {
            model.SetDirty();
        }
        return false;
    }

    /// <summary>
    ///     Tells everyone the timers of a machine this client simulates, when <see cref="ShouldBroadcast" /> says they
    ///     are due. Called after each of the machine's updates, which the game runs once a second.
    /// </summary>
    public void BroadcastIfSimulated(FiltrationMachine machine)
    {
        if (!machine.TryGetNitroxId(out NitroxId machineId) ||
            !FiltersHere(true, simulationOwnership.HasAnyLockType(machineId), BasePowerSources.IsRebuildingBases(), IsLoaded()))
        {
            return;
        }
        if (TryCreateUpdate(machineId, GetState(machine), Time.realtimeSinceStartup, out EntityMetadataUpdate update))
        {
            packetSender.Send(update);
        }
    }

    /// <summary>
    ///     Returns the update to send for a simulated machine, and remembers it as sent, when <see cref="ShouldBroadcast" />
    ///     says it is due.
    /// </summary>
    internal bool TryCreateUpdate(NitroxId machineId, FiltrationMachineMetadata state, float now, out EntityMetadataUpdate update)
    {
        update = null;
        Broadcast? last = lastBroadcastByMachine.TryGetValue(machineId, out Broadcast previous) ? previous : null;
        if (!ShouldBroadcast(last, now, state.TimeRemainingWater, state.TimeRemainingSalt))
        {
            return false;
        }
        lastBroadcastByMachine[machineId] = new Broadcast(now, state.TimeRemainingWater, state.TimeRemainingSalt);
        update = new EntityMetadataUpdate(machineId, state);
        return true;
    }

    /// <summary>
    ///     Stands in for FiltrationMachine.Spawn, which puts a new bottle of water or lump of salt into the machine.
    ///     <para>
    ///     The game picks the new item up before it puts it away. Nitrox would report that as an item the player picked
    ///     up: the others would get it in this player's inventory and then see it move into the machine, if it had
    ///     spawned by then. The item is reported once, as an inventory item of the machine, instead.
    ///     </para>
    /// </summary>
    /// <param name="spawned">Set to what the game's method returns: whether an item was made.</param>
    /// <returns>False if the game's own method must not run.</returns>
    public bool LetGameSpawn(FiltrationMachine machine, GameObject prefab, ref bool spawned)
    {
        if (!machine.TryGetNitroxId(out NitroxId machineId))
        {
            return true;
        }
        spawned = false;
        // Also refuses the game's filterwater and filtersalt commands of a player who does not simulate the machine
        if (!FiltersHere(true, simulationOwnership.HasAnyLockType(machineId), BasePowerSources.IsRebuildingBases(), IsLoaded()))
        {
            return false;
        }

        // From here on what FiltrationMachine.Spawn does
        Vector2int size = TechData.GetItemSize(prefab.GetComponent<Pickupable>().GetTechType());
        ItemsContainer container = machine.storageContainer.container;
        if (!container.HasRoomFor(size.x, size.y))
        {
            return false;
        }
        Pickupable item = Object.Instantiate(prefab).GetComponent<Pickupable>();
        using (PacketSuppressor<PickupItem>.Suppress())
        {
            item.Pickup(false);
        }
        using (PacketSuppressor<EntityReparented>.Suppress())
        {
            container.UnsafeAdd(new InventoryItem(item));
        }
        items.MovedIntoInventory(item.gameObject, machineId);
        spawned = true;
        return false;
    }
}
