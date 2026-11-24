using Nitrox.Model.DataStructures;
using NitroxClient.MonoBehaviours;

namespace NitroxClient.GameLogic;

/// <summary>
///     Reports the oxygen in a tank while it is worn, so that a tank emptied by diving is not full again after the player
///     rejoins or the server restarts. A tank's oxygen is otherwise only sent when it is taken off.
/// </summary>
public class OxygenTanks
{
    private readonly Entities entities;

    public OxygenTanks(Entities entities)
    {
        this.entities = entities;
    }

    /// <summary>
    ///     Called after oxygen was added to or taken from a tank.
    /// </summary>
    public void Changed(Oxygen oxygen, float previousOxygen)
    {
        // The player's own lungs are no item
        if (oxygen.isPlayer || !BatteryCharges.ShouldReport(previousOxygen, oxygen.oxygenAvailable, oxygen.oxygenCapacity) || !oxygen.TryGetNitroxId(out NitroxId id))
        {
            return;
        }
        if (!entities.SpawningEntities && entities.IsKnownEntity(id))
        {
            entities.EntityMetadataChanged(oxygen, id);
        }
    }

    /// <summary>
    ///     Called after the player's air bladder was filled or let air out. The others see it as full as it is, and it
    ///     keeps its air after a rejoin (<see cref="Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata.AirBladderMetadata" />).
    /// </summary>
    public void AirBladderChanged(AirBladder airBladder, float previousOxygen)
    {
        // Reported per unit, and when it is full or empty; a bladder holds a few units only
        if (!BatteryCharges.ShouldReport(previousOxygen, airBladder.oxygen, airBladder.oxygenCapacity) || !airBladder.TryGetNitroxId(out NitroxId id))
        {
            return;
        }
        if (!entities.SpawningEntities && entities.IsKnownEntity(id))
        {
            entities.EntityMetadataChanged(airBladder, id);
        }
    }
}
