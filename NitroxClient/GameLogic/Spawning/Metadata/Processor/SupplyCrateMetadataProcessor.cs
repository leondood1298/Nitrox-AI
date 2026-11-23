using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class SupplyCrateMetadataProcessor : EntityMetadataProcessor<SupplyCrateMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, SupplyCrateMetadata metadata)
    {
        if (!gameObject.TryGetComponent(out SupplyCrate crate))
        {
            Log.Error($"[{nameof(SupplyCrateMetadataProcessor)}] Could not find a SupplyCrate on {gameObject.name}");
            return;
        }

        // A crate that has to be cut open keeps its cut state in this metadata as well
        if (gameObject.TryGetComponent(out Sealed sealedCrate))
        {
            SealedDoorMetadataProcessor.ApplySealedState(sealedCrate, metadata.Sealed, metadata.OpenedAmount);
        }

        // The game never closes a crate once it is open
        if (!metadata.Open || crate.open)
        {
            return;
        }

        Animation animation = crate.GetComponentInChildren<Animation>();
        bool initialSyncCompleted = Multiplayer.Main && Multiplayer.Main.InitialSyncCompleted;
        if (animation && IsOpenedInSight(initialSyncCompleted, Resolve<Entities>().SpawningEntities))
        {
            // Lid animation and sound as for the player who opened it; the sound is not one the game sends to others
            crate.ToggleOpenState();
            return;
        }

        crate.open = true;
        // The game keeps this object's state in its own save, which a Nitrox world does not use
        if (crate.setActiveOnOpen)
        {
            crate.setActiveOnOpen.SetActive(true);
        }
        // What SupplyCrate.Start does with a crate loaded open. Start also runs it for a crate that has just spawned.
        if (animation)
        {
            animation.Play(crate.snapOpenOnLoad);
        }
    }

    /// <summary>
    ///     Whether the crate opens in front of a player who already plays, rather than being loaded already open: only
    ///     then the opening is shown and heard.
    /// </summary>
    /// <param name="initialSyncCompleted">Whether this player has finished joining.</param>
    /// <param name="spawningEntities">Whether entities are being spawned, as when a cell is loaded.</param>
    internal static bool IsOpenedInSight(bool initialSyncCompleted, bool spawningEntities)
    {
        return initialSyncCompleted && !spawningEntities;
    }
}
