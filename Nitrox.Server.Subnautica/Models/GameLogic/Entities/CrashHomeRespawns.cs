using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Server.Subnautica.Models.Packets.Core;
using Nitrox.Server.Subnautica.Services;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

/// <summary>
///     The server's side of a crash home and its crashfish. The crashfish is a stored entity of its cell: unloading the
///     home or the fish never removes it, and the home is paired with it again when both are loaded (see the client's
///     CrashHomes). Only when the crashfish itself is gone does its home wait <see cref="RESPAWN_DELAY_SECONDS" /> and
///     make a new one, as in the game.
/// </summary>
/// <remarks>
///     A crashfish leaves the server in many ways (it explodes or is killed, its corpse goes, it is destroyed by a game
///     script), each a different packet. They all end in <see cref="EntityRegistry.RemoveEntity" />, so the home is
///     released there, through <see cref="EntityRegistry.EntityRemoved" />, and not by each packet's processor: one that
///     forgot would leave the home naming a dead fish for good.
/// </remarks>
internal sealed class CrashHomeRespawns
{
    /// <summary>
    ///     CrashHome.respawnDelay.
    /// </summary>
    public const float RESPAWN_DELAY_SECONDS = 1200f;

    private static readonly NitroxTechType crashTechType = new("Crash");

    private readonly EntityRegistry entityRegistry;
    private readonly ILogger<CrashHomeRespawns> logger;
    private readonly IPacketSender packetSender;
    private readonly TimeService timeService;

    public CrashHomeRespawns(EntityRegistry entityRegistry, TimeService timeService, IPacketSender packetSender, ILogger<CrashHomeRespawns> logger)
    {
        this.entityRegistry = entityRegistry;
        this.timeService = timeService;
        this.packetSender = packetSender;
        this.logger = logger;
        entityRegistry.EntityRemoved += OnEntityRemoved;
    }

    /// <inheritdoc cref="RepairAfterLoad(IReadOnlyCollection{Entity})" />
    public int RepairAfterLoad() => RepairAfterLoad(entityRegistry.GetAllEntities());

    /// <summary>
    ///     Gives each home whose crashfish was <paramref name="crashId" /> a new one after the game's delay.
    /// </summary>
    /// <returns>The homes changed.</returns>
    internal static List<Entity> ReleaseHomesOf(IEnumerable<Entity> entities, NitroxId crashId, float now)
    {
        List<Entity> homes = [.. entities.Where(entity => entity.Metadata is CrashHomeMetadata { SpawnedCrashId.HasValue: true } metadata && metadata.SpawnedCrashId.Value.Equals(crashId))];
        foreach (Entity home in homes)
        {
            home.Metadata = new CrashHomeMetadata(now + RESPAWN_DELAY_SECONDS, Optional.Empty);
        }
        return homes;
    }

    /// <summary>
    ///     A home that has no crashfish and none on the way (its fish is not in the world, or it never recorded one) makes
    ///     one at once when next loaded, as the game's CrashHome.Start does. Saves of builds that released a home only for
    ///     some of the ways a fish can go have such homes.
    /// </summary>
    /// <returns>How many homes were repaired.</returns>
    internal static int RepairAfterLoad(IReadOnlyCollection<Entity> entities)
    {
        // Only a crashfish counts: an id that is now something else does not keep the home waiting
        HashSet<NitroxId> crashIds = [.. entities.Where(IsCrash).Select(entity => entity.Id)];
        int repaired = 0;
        foreach (Entity home in entities)
        {
            if (home.Metadata is not CrashHomeMetadata metadata || metadata.SpawnTime >= 0)
            {
                continue;
            }
            if (metadata.SpawnedCrashId.HasValue && crashIds.Contains(metadata.SpawnedCrashId.Value))
            {
                continue;
            }
            home.Metadata = new CrashHomeMetadata(0, Optional.Empty);
            repaired++;
        }
        return repaired;
    }

    private static bool IsCrash(Entity entity) => crashTechType.Equals(entity.TechType);

    private void OnEntityRemoved(Entity removed)
    {
        if (!IsCrash(removed))
        {
            return;
        }
        float now = (float)timeService.GameTime.TotalSeconds;
        List<Entity> homes = ReleaseHomesOf(entityRegistry.GetAllEntities(), removed.Id, now);
        foreach (Entity home in homes)
        {
            logger.ZLogInformation($"Crashfish {removed.Id} is gone: its home {home.Id} makes the next one at {now + RESPAWN_DELAY_SECONDS:F0} s (now {now:F0} s)");
        }
        if (homes.Count > 0)
        {
            _ = BroadcastAsync(homes);
        }
    }

    /// <summary>
    ///     Tells every player when the released homes make their next crashfish. The removal that released them runs
    ///     inside the registry and cannot wait for the network.
    /// </summary>
    private async Task BroadcastAsync(List<Entity> homes)
    {
        try
        {
            foreach (Entity home in homes)
            {
                await packetSender.SendPacketToAllAsync(new EntityMetadataUpdate(home.Id, home.Metadata));
            }
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"Could not tell the players about {homes.Count} released crash homes");
        }
    }
}
