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
internal sealed class CrashHomeRespawns(EntityRegistry entityRegistry, TimeService timeService, IPacketSender packetSender)
{
    /// <summary>
    ///     CrashHome.respawnDelay.
    /// </summary>
    public const float RESPAWN_DELAY_SECONDS = 1200f;

    private static readonly NitroxTechType crashTechType = new("Crash");

    private readonly EntityRegistry entityRegistry = entityRegistry;
    private readonly IPacketSender packetSender = packetSender;
    private readonly TimeService timeService = timeService;

    /// <summary>
    ///     Tells every player when the home of a removed crashfish makes its next one.
    /// </summary>
    public async Task EntityRemovedAsync(Entity removed)
    {
        if (!crashTechType.Equals(removed.TechType))
        {
            return;
        }
        foreach (Entity home in ReleaseHomesOf(entityRegistry.GetAllEntities(), removed.Id, (float)timeService.GameTime.TotalSeconds))
        {
            await packetSender.SendPacketToAllAsync(new EntityMetadataUpdate(home.Id, home.Metadata));
        }
    }

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
    ///     one at once when next loaded, as the game's CrashHome.Start does. Older saves have such homes: their fish were
    ///     deleted whenever a player unloaded the home.
    /// </summary>
    /// <returns>How many homes were repaired.</returns>
    internal static int RepairAfterLoad(IReadOnlyCollection<Entity> entities)
    {
        HashSet<NitroxId> ids = [.. entities.Select(entity => entity.Id)];
        int repaired = 0;
        foreach (Entity home in entities)
        {
            if (home.Metadata is not CrashHomeMetadata metadata || metadata.SpawnTime >= 0)
            {
                continue;
            }
            if (metadata.SpawnedCrashId.HasValue && ids.Contains(metadata.SpawnedCrashId.Value))
            {
                continue;
            }
            home.Metadata = new CrashHomeMetadata(0, Optional.Empty);
            repaired++;
        }
        return repaired;
    }
}
