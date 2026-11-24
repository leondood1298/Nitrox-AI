using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;

namespace Nitrox.Server.Subnautica.Models.GameLogic;

/// <summary>
///     Decides who gets an item when two games take it at the same moment. Each game gives the item to its player at once
///     and tells the server afterwards; the first to tell the server gets it, and the others are corrected.
/// </summary>
/// <remarks>
///     Two kinds of items are decided here:
///     <list type="bullet">
///         <item>
///             Items every game knows by the same id (an item lying in the world, in a locker, in a slot): whoever holds
///             it on the server keeps it. An item another player holds may not be taken, moved, dropped or destroyed by
///             anyone else (see <see cref="TryGetOtherHolder" />).
///         </item>
///         <item>
///             Items a game makes new from a source (see <see cref="ItemSource" />): the first claim on a source and token
///             wins, the others are removed from the games that made them (see <see cref="TryClaimSource" />).
///         </item>
///     </list>
/// </remarks>
internal sealed class ItemClaims
{
    /// <summary>
    ///     How long a claim on a source is remembered. Much longer than the time two packets of the same moment take to
    ///     arrive, much shorter than any source takes to give the same thing again (a medkit, a fruit).
    /// </summary>
    public static readonly TimeSpan SOURCE_CLAIM_LIFETIME = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     How long a fabricator's result is kept for the player who crafted it once it is done.
    /// </summary>
    public static readonly TimeSpan CRAFT_RESERVATION = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     How long a picked fruit cannot be reported grown back. Much longer than a report takes to arrive, much shorter
    ///     than any plant takes to grow a fruit back (150 seconds and more).
    /// </summary>
    public static readonly TimeSpan RECENT_PICK = TimeSpan.FromSeconds(10);

    private const string SOMEONE = "Someone";

    private readonly EntityRegistry entityRegistry;
    private readonly PlayerManager playerManager;
    private readonly Func<TimeSpan> clock;
    private readonly Lock locker = new();
    private readonly Dictionary<(NitroxId SourceId, string Token), SourceClaim> sourceClaims = [];
    private readonly Dictionary<(NitroxId SourceId, string PlayerName), TimeSpan> lostClaims = [];
    private readonly Dictionary<NitroxId, Craft> crafts = [];
    private readonly Dictionary<(NitroxId ParentId, string Slot), string> slotInstallers = [];
    private readonly Dictionary<(NitroxId PlantId, int FruitIndex), TimeSpan> recentPicks = [];

    public ItemClaims(EntityRegistry entityRegistry, PlayerManager playerManager) : this(entityRegistry, playerManager, CreateClock())
    {
    }

    internal ItemClaims(EntityRegistry entityRegistry, PlayerManager playerManager, Func<TimeSpan> clock)
    {
        this.entityRegistry = entityRegistry;
        this.playerManager = playerManager;
        this.clock = clock;
    }

    /// <summary>
    ///     Finds the player, other than <paramref name="sender" />, whose inventory (or a container in it, or a tool in it)
    ///     holds <paramref name="entity" />.
    /// </summary>
    public bool TryGetOtherHolder(Entity entity, Player sender, [NotNullWhen(true)] out string? holderName)
    {
        NitroxId? holderId = FindHoldingPlayerId(entity);
        if (holderId == null || holderId == sender.GameObjectId)
        {
            holderName = null;
            return false;
        }
        holderName = NameOfPlayer(holderId);
        return true;
    }

    /// <summary>
    ///     The id of the player entity above <paramref name="entity" />, not counting the entity itself: a player who
    ///     sits in a vehicle does not hold the vehicle, but an item in a player's inventory has that player above it.
    /// </summary>
    public NitroxId? FindHoldingPlayerId(Entity entity)
    {
        HashSet<NitroxId> visited = [entity.Id];
        NitroxId? parentId = entity.ParentId;
        while (parentId != null && visited.Add(parentId) && entityRegistry.TryGetEntityById(parentId, out Entity? parent))
        {
            if (parent is PlayerEntity)
            {
                return parent.Id;
            }
            parentId = parent.ParentId;
        }
        return null;
    }

    /// <summary>
    ///     A fruit (seed, sample) of a plant was picked: for a moment, a report that it grew back is a game that has not
    ///     heard of the pick yet (see <see cref="Entities.PlantMetadataAuthority" />).
    /// </summary>
    public void NotePicked(NitroxId plantId, int fruitIndex)
    {
        lock (locker)
        {
            TimeSpan now = clock();
            Forget(now);
            recentPicks[(plantId, fruitIndex)] = now;
        }
    }

    /// <summary>
    ///     Whether the fruit at <paramref name="fruitIndex" /> was picked less than <see cref="RECENT_PICK" /> ago.
    /// </summary>
    public bool PickedLately(NitroxId plantId, int fruitIndex)
    {
        lock (locker)
        {
            return recentPicks.TryGetValue((plantId, fruitIndex), out TimeSpan pickedAt) && clock() - pickedAt < RECENT_PICK;
        }
    }

    /// <summary>
    ///     A player's game made an item from <paramref name="source" />. The first player to claim a source and token gets
    ///     it; a crafter's result is its crafter's alone for <see cref="CRAFT_RESERVATION" /> once it is done.
    /// </summary>
    /// <returns>True if <paramref name="sender" /> gets the item.</returns>
    public bool TryClaimSource(ItemSource source, Player sender, [NotNullWhen(false)] out string? winnerName, out bool reserved)
    {
        lock (locker)
        {
            TimeSpan now = clock();
            Forget(now);
            reserved = false;

            if (crafts.TryGetValue(source.SourceId, out Craft? craft) && craft.CrafterName != sender.Name && now < craft.ReadyAt + CRAFT_RESERVATION)
            {
                winnerName = craft.CrafterName;
                reserved = true;
                lostClaims[(source.SourceId, sender.Name)] = now;
                return false;
            }

            (NitroxId, string) key = (source.SourceId, source.Token ?? "");
            if (sourceClaims.TryGetValue(key, out SourceClaim? claim) && claim.PlayerName != sender.Name)
            {
                winnerName = claim.PlayerName;
                lostClaims[(source.SourceId, sender.Name)] = now;
                return false;
            }
            sourceClaims[key] = new SourceClaim(sender.Name, now);
            winnerName = null;
            return true;
        }
    }

    /// <summary>
    ///     Whether <paramref name="sender" /> lately lost a claim on <paramref name="sourceId" />: the change its game then
    ///     makes to the source (a fabricator emptied) must not be kept.
    /// </summary>
    public bool LostClaimLately(NitroxId sourceId, Player sender)
    {
        lock (locker)
        {
            TimeSpan now = clock();
            Forget(now);
            return lostClaims.ContainsKey((sourceId, sender.Name));
        }
    }

    /// <summary>
    ///     Decides a fabricator's state that a player's game reports. A new craft may only start on an empty fabricator; a
    ///     result may only be taken by whoever it is kept for.
    /// </summary>
    /// <param name="stored">The fabricator's state on the server.</param>
    /// <returns>True if the update is kept.</returns>
    public bool AcceptCrafterUpdate(NitroxId crafterId, CrafterMetadata? stored, CrafterMetadata update, Player sender)
    {
        bool startsCraft = update.Duration > 0 && update.TechType != null && update.TechType.Name != "None" && update.Amount > 0;
        if (startsCraft)
        {
            if (stored != null && stored.TechType != null && stored.TechType.Name != "None" && stored.Amount > 0 && stored.StartTime != update.StartTime)
            {
                return false;
            }
            lock (locker)
            {
                crafts[crafterId] = new Craft(sender.Name, clock() + TimeSpan.FromSeconds(update.Duration));
            }
            return true;
        }

        lock (locker)
        {
            TimeSpan now = clock();
            Forget(now);
            if (lostClaims.ContainsKey((crafterId, sender.Name)))
            {
                return false;
            }
            if (crafts.TryGetValue(crafterId, out Craft? craft) && craft.CrafterName != sender.Name && now < craft.ReadyAt + CRAFT_RESERVATION)
            {
                return false;
            }
            return true;
        }
    }

    /// <summary>
    ///     Decides whether a module may be put into a slot of an equipment (a vehicle's upgrade, a charger's battery): one
    ///     slot holds one item.
    /// </summary>
    /// <returns>True if the slot is free or already holds this module.</returns>
    public bool TryTakeSlot(InstalledModuleEntity module, Player sender, [NotNullWhen(false)] out string? occupantInstaller)
    {
        if (module.ParentId != null && entityRegistry.TryGetEntityById(module.ParentId, out Entity? parent) &&
            parent.ChildEntities.OfType<InstalledModuleEntity>().Any(other => other.Slot == module.Slot && other.Id != module.Id))
        {
            lock (locker)
            {
                occupantInstaller = slotInstallers.GetValueOrDefault((module.ParentId, module.Slot), SOMEONE);
            }
            return false;
        }
        lock (locker)
        {
            if (module.ParentId != null)
            {
                slotInstallers[(module.ParentId, module.Slot)] = sender.Name;
            }
        }
        occupantInstaller = null;
        return true;
    }

    /// <summary>
    ///     Decides whether a battery or power cell may be put into a slot of a tool or vehicle: one slot holds one.
    /// </summary>
    /// <returns>True if the slot is free or already holds this battery.</returns>
    public bool TryTakeBatterySlot(InstalledBatteryEntity battery, [NotNullWhen(false)] out string? occupantHolder)
    {
        if (battery.ParentId != null && entityRegistry.TryGetEntityById(battery.ParentId, out Entity? parent) &&
            parent.ChildEntities.OfType<InstalledBatteryEntity>().Any(other => other.ComponentIndex == battery.ComponentIndex && other.Id != battery.Id))
        {
            occupantHolder = SOMEONE;
            return false;
        }
        occupantHolder = null;
        return true;
    }

    public string NameOfPlayer(NitroxId? playerId)
    {
        return playerManager.GetAllPlayers().FirstOrDefault(player => player.GameObjectId == playerId)?.Name ?? SOMEONE;
    }

    private void Forget(TimeSpan now)
    {
        foreach ((NitroxId, string) key in sourceClaims.Where(pair => now - pair.Value.Time > SOURCE_CLAIM_LIFETIME).Select(pair => pair.Key).ToList())
        {
            sourceClaims.Remove(key);
        }
        foreach ((NitroxId, string) key in lostClaims.Where(pair => now - pair.Value > SOURCE_CLAIM_LIFETIME).Select(pair => pair.Key).ToList())
        {
            lostClaims.Remove(key);
        }
        foreach (NitroxId key in crafts.Where(pair => now > pair.Value.ReadyAt + CRAFT_RESERVATION).Select(pair => pair.Key).ToList())
        {
            crafts.Remove(key);
        }
        foreach ((NitroxId, int) key in recentPicks.Where(pair => now - pair.Value >= RECENT_PICK).Select(pair => pair.Key).ToList())
        {
            recentPicks.Remove(key);
        }
    }

    private static Func<TimeSpan> CreateClock()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        return () => stopwatch.Elapsed;
    }

    private sealed record SourceClaim(string PlayerName, TimeSpan Time);

    private sealed record Craft(string CrafterName, TimeSpan ReadyAt);
}
