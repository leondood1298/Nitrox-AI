using Nitrox.Model.DataStructures;
using NitroxClient.MonoBehaviours;

namespace NitroxClient.GameLogic;

/// <summary>
///     Shares what a player does with a beacon that the item flow does not carry: its label and its humming.
/// </summary>
public class Beacons
{
    private readonly Entities entities;

    /// <summary>
    ///     Inside Beacon.Start, which sets the label the beacon already has (from its metadata, or the default).
    /// </summary>
    private bool starting;

    public Beacons(Entities entities)
    {
        this.entities = entities;
    }

    /// <summary>
    ///     A label is sent from where it is set, so that every way of entering it is covered (the game's text box, a VR
    ///     keyboard), but only when it changed and only after joining: Beacon.Start and loading set the label it already
    ///     had, and a label applied from the server is not sent back (the metadata processor suppresses it).
    /// </summary>
    internal static bool ShouldBroadcastLabel(string oldLabel, string newLabel, bool starting, bool joined)
    {
        return joined && !starting && oldLabel != newLabel;
    }

    public void StartBegins() => starting = true;

    public void StartEnds() => starting = false;

    public void LabelSet(BeaconLabel beaconLabel, string oldLabel)
    {
        if (!ShouldBroadcastLabel(oldLabel, beaconLabel.GetLabel(), starting, IsJoined()))
        {
            return;
        }
        Beacon beacon = beaconLabel.GetComponentInParent<Beacon>();
        if (beacon && beacon.TryGetIdOrWarn(out NitroxId id))
        {
            entities.EntityMetadataChanged(beacon, id);
        }
    }

    /// <summary>
    ///     A beacon hums once thrown (Beacon.Throw plays its loop), in the thrower's game only. A beacon that appears in the
    ///     world while playing was thrown by another player: it hums here as well. Beacons loaded with the world stay
    ///     silent, as in the game.
    /// </summary>
    public void Started(Beacon beacon)
    {
        if (!IsJoined() || !beacon.beaconOnLoop || beacon.beaconOnLoop.playing)
        {
            return;
        }
        Pickupable pickupable = beacon.pickupable;
        if (pickupable && (pickupable.attached || pickupable.inventoryItem != null))
        {
            return;
        }
        beacon.beaconOnLoop.Play();
    }

    private static bool IsJoined() => Multiplayer.Main && Multiplayer.Main.InitialSyncCompleted;
}
