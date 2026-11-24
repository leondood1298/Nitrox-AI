using System.Collections;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;
using UWE;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

/// <summary>
///     Opens or closes a Cyclops door as the server keeps it, for a player who joins or whose Cyclops is spawned.
/// </summary>
public class OpenableMetadataProcessor : EntityMetadataProcessor<OpenableMetadata>
{
    /// <summary>
    ///     A short swing rather than a jump, as the game has no way to set a door without animating it.
    /// </summary>
    private const float SWING_DURATION = 0.1f;

    public override void ProcessMetadata(GameObject gameObject, OpenableMetadata metadata)
    {
        if (!gameObject.TryGetComponent(out Openable openable))
        {
            Log.Error($"Could not find Openable on {gameObject.name}");
            return;
        }
        CoroutineHost.StartCoroutine(ApplyAfterStart(openable, metadata.IsOpen));
    }

    /// <remarks>
    ///     Openable.Start takes the door's open and closed rotations from where it is and from its open flag: the state is
    ///     applied after it ran, as a swing.
    /// </remarks>
    private static IEnumerator ApplyAfterStart(Openable openable, bool isOpen)
    {
        yield return null;
        if (!openable || openable.isOpen == isOpen)
        {
            yield break;
        }
        using (PacketSuppressor<OpenableStateChanged>.Suppress())
        {
            openable.PlayOpenAnimation(isOpen, SWING_DURATION);
        }
    }
}
