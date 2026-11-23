using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata;
using NitroxClient.MonoBehaviours;

namespace NitroxClient.GameLogic;

/// <summary>
///     Sends a medical cabinet's state to the server, which keeps it for the cabinet's entity: the game would otherwise
///     start the timer of the next medkit anew whenever the cabinet is loaded.
/// </summary>
public class MedicalCabinets
{
    private readonly Entities entities;
    private readonly EntityMetadataManager entityMetadataManager;

    public MedicalCabinets(Entities entities, EntityMetadataManager entityMetadataManager)
    {
        this.entities = entities;
        this.entityMetadataManager = entityMetadataManager;
    }

    /// <summary>
    ///     Called after the local player clicked the cabinet and after a medkit appeared in it. Every game makes the
    ///     medkit at the same game time; the copies of that state are the same.
    /// </summary>
    public void BroadcastState(MedicalCabinet cabinet)
    {
        // While joining, a cabinet still runs on the timer the game started when loading it, not on the stored one.
        if (!Multiplayer.Main || !Multiplayer.Main.InitialSyncCompleted)
        {
            return;
        }
        if (cabinet.TryGetIdOrWarn(out NitroxId id) && entityMetadataManager.TryExtract(cabinet, out EntityMetadata metadata))
        {
            entities.BroadcastMetadataUpdate(id, metadata);
        }
    }
}
