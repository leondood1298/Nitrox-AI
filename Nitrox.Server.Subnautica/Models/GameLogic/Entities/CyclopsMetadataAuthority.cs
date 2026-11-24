using System.Collections.Generic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

/// <summary>
///     Decides which parts of a Cyclops metadata update the server accepts. Every update carries the whole metadata, from
///     the sender's copy of the Cyclops.
///     <list type="bullet">
///         <item>The helm controls (engine, speed mode, silent running, shield, sonar) are the pilot's. While nobody sits at
///         the helm, the player simulating the Cyclops sets them (the game switches them off by itself, e.g. without power).
///         Another player's copy of them can be out of date: a hull report sent before the pilot's press reached that player
///         would undo the press.</item>
///         <item>Health and destruction are only simulated by the player holding the Cyclops' simulation lock.</item>
///     </list>
/// </summary>
internal static class CyclopsMetadataAuthority
{
    /// <param name="senderSimulatesCyclops">The sender holds the Cyclops' simulation lock, or nobody does.</param>
    /// <param name="senderControlsHelm">The sender pilots the Cyclops, or nobody does and the sender simulates it.</param>
    public static CyclopsMetadata Merge(CyclopsMetadata? stored, CyclopsMetadata requested, bool senderSimulatesCyclops, bool senderControlsHelm)
    {
        if (stored == null || (senderSimulatesCyclops && senderControlsHelm))
        {
            return requested;
        }
        CyclopsMetadata helm = senderControlsHelm ? requested : stored;
        CyclopsMetadata hull = senderSimulatesCyclops ? requested : stored;
        return new CyclopsMetadata(helm.SilentRunningOn, helm.ShieldOn, helm.SonarOn, helm.EngineOn, helm.EngineMode, hull.Health, hull.IsDestroyed);
    }

    /// <returns>True when the sender's copy of the helm controls was not taken, so the sender must be given the kept ones.</returns>
    public static bool HelmWasRefused(CyclopsMetadata requested, CyclopsMetadata accepted)
    {
        return requested.SilentRunningOn != accepted.SilentRunningOn || requested.ShieldOn != accepted.ShieldOn || requested.SonarOn != accepted.SonarOn ||
               requested.EngineOn != accepted.EngineOn || requested.EngineMode != accepted.EngineMode;
    }

    public static bool IsNewlyDestroyed(CyclopsMetadata? stored, CyclopsMetadata accepted)
    {
        return accepted.IsDestroyed && (stored == null || !stored.IsDestroyed);
    }

    /// <summary>
    ///     The vehicles docked in a Cyclops: in the game they die with it (its "OnKill" reaches them).
    /// </summary>
    public static List<VehicleEntity> FindDockedVehicles(Entity cyclops)
    {
        List<VehicleEntity> found = [];
        Stack<Entity> toVisit = new(cyclops.ChildEntities);
        while (toVisit.Count > 0)
        {
            Entity entity = toVisit.Pop();
            if (entity is VehicleEntity vehicle)
            {
                found.Add(vehicle);
                continue;
            }
            foreach (Entity child in entity.ChildEntities)
            {
                toVisit.Push(child);
            }
        }
        return found;
    }
}
