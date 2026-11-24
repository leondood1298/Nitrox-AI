using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
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
    private readonly IPacketSender packetSender;
    private readonly ItemSources itemSources;

    /// <summary>
    ///     The cabinets whose timer this game has reported, once per loaded cabinet.
    /// </summary>
    private readonly HashSet<MedicalCabinet> timersReported = [];

    public MedicalCabinets(Entities entities, EntityMetadataManager entityMetadataManager, IPacketSender packetSender, ItemSources itemSources)
    {
        this.entities = entities;
        this.entityMetadataManager = entityMetadataManager;
        this.packetSender = packetSender;
        this.itemSources = itemSources;
    }

    /// <summary>
    ///     Called after MedicalCabinet.Update.
    /// </summary>
    /// <param name="hadMedKit">Whether the cabinet held a medkit before the update.</param>
    public void Updated(MedicalCabinet cabinet, bool hadMedKit)
    {
        // While joining, a cabinet still runs on the timer the game started when loading it, not on the stored one.
        if (!Multiplayer.Main || !Multiplayer.Main.InitialSyncCompleted)
        {
            return;
        }
        ReportTimerOnce(cabinet);
        if (!hadMedKit && cabinet.hasMedKit)
        {
            BroadcastState(cabinet);
        }
    }

    /// <summary>
    ///     Until a cabinet is clicked or its medkit appears, the server has no state for it and each game would run the
    ///     timer it started on its own clock. The server keeps the first timer a game reports and gives it to the others.
    /// </summary>
    private void ReportTimerOnce(MedicalCabinet cabinet)
    {
        if (timersReported.Contains(cabinet) || !cabinet.TryGetNitroxId(out NitroxId id))
        {
            return;
        }
        // MedicalCabinet.Start has not run yet when there is no state
        if (entityMetadataManager.TryExtract(cabinet, out EntityMetadata metadata) && metadata is MedicalCabinetMetadata state)
        {
            timersReported.Add(cabinet);
            packetSender.Send(new MedicalCabinetTimerStarted(id, state));
        }
    }

    /// <summary>
    ///     Called before the local player clicks the cabinet. A click on an open cabinet with a medkit gives the medkit,
    ///     which two players can take at the same moment: the server gives it to one of them (see <see cref="ItemSources" />).
    /// </summary>
    public void Clicking(MedicalCabinet cabinet)
    {
        if (cabinet.doorOpen && cabinet.hasMedKit && cabinet.TryGetNitroxId(out NitroxId id))
        {
            itemSources.Expect(TechType.FirstAidKit, id, "medkit");
        }
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
