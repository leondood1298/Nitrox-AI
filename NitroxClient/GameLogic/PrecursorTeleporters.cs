using System.Collections.Generic;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;
using NitroxClient.Communication.Abstract;

namespace NitroxClient.GameLogic;

/// <summary>
///     Keeps the game's set of active teleporters (TeleporterManager) the same for every player. A teleporter reads it when
///     it spawns, and only the teleporters loaded for the activating player hear of an activation.
/// </summary>
public class PrecursorTeleporters
{
    private readonly IPacketSender packetSender;

    public PrecursorTeleporters(IPacketSender packetSender)
    {
        this.packetSender = packetSender;
    }

    /// <summary>
    ///     To be called before the game adds <paramref name="identifier" /> to its active teleporters.
    /// </summary>
    public void BroadcastActivation(string identifier)
    {
        if (TeleporterManager.main && !TeleporterManager.main.activeTeleporters.Contains(identifier))
        {
            packetSender.Send(new TeleporterActivated(identifier));
        }
    }

    /// <summary>
    ///     Another player activated a teleporter pair: the ends loaded here open as they would for that player.
    /// </summary>
    public void ActivateFromRemote(string identifier)
    {
        if (!TeleporterManager.main)
        {
            Log.Error($"[{nameof(PrecursorTeleporters)}] No TeleporterManager to activate teleporter {identifier}");
            return;
        }

        // The loaded teleporters report their opening themselves, and the player who activated them already did
        using (PacketSuppressor<TeleporterActivated>.Suppress())
        using (PacketSuppressor<EntityMetadataUpdate>.Suppress())
        {
            TeleporterManager.ActivateTeleporter(identifier);
        }
    }

    /// <summary>
    ///     For a joining player, before any teleporter has spawned and read the set.
    /// </summary>
    public void SetActive(IEnumerable<string> identifiers)
    {
        if (!TeleporterManager.main)
        {
            Log.Error($"[{nameof(PrecursorTeleporters)}] No TeleporterManager to restore the active teleporters in");
            return;
        }

        foreach (string identifier in identifiers)
        {
            TeleporterManager.main.activeTeleporters.Add(identifier);
        }
    }
}
