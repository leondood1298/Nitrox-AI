using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     The trail of a pathfinder tool (DiveReel) is its metadata (<see cref="DiveReelMetadata" />): the player who lays or
///     reels it in sends it, and every game shows the nodes of every tool it has, in a player's hands, a locker or the
///     world. In single player the trail is saved with the tool; here the server keeps it.
/// </summary>
public class DiveReels
{
    private readonly Entities entities;

    /// <summary>
    ///     The nodes this game made for a tool's trail, by the tool's id: a tool that is spawned again (taken by another
    ///     player) shows its trail again, and the nodes of the old copy are removed.
    /// </summary>
    private readonly Dictionary<NitroxId, List<DiveReelNode>> shownNodes = [];

    public DiveReels(Entities entities)
    {
        this.entities = entities;
    }

    /// <summary>
    ///     Called after this player laid a node or reeled the trail in.
    /// </summary>
    public void TrailChanged(DiveReel diveReel)
    {
        if (diveReel.TryGetNitroxId(out NitroxId id) && entities.IsKnownEntity(id))
        {
            entities.EntityMetadataChanged(diveReel, id);
        }
    }

    /// <summary>
    ///     Shows a tool's trail as the server has it, as DiveReel.OnProtoDeserialize does when a game is loaded.
    /// </summary>
    public void ShowTrail(DiveReel diveReel, DiveReelMetadata metadata)
    {
        RemoveShownNodes(diveReel);

        diveReel.nodePositions = [];
        foreach (Nitrox.Model.DataStructures.Unity.NitroxVector3 position in metadata.NodePositions ?? [])
        {
            diveReel.nodePositions.Add(position.ToUnity());
        }

        bool isFirst = true;
        foreach (Vector3 position in diveReel.nodePositions)
        {
            // A reeled-in node is kept as a zero position
            if (position == Vector3.zero)
            {
                continue;
            }
            diveReel.CreateNewNode(position, isFirst, true);
            isFirst = false;
        }

        if (diveReel.TryGetNitroxId(out NitroxId id))
        {
            shownNodes[id] = [.. diveReel.nodes];
        }
    }

    private void RemoveShownNodes(DiveReel diveReel)
    {
        if (diveReel.TryGetNitroxId(out NitroxId id) && shownNodes.TryGetValue(id, out List<DiveReelNode> nodes))
        {
            foreach (DiveReelNode node in nodes)
            {
                if (node)
                {
                    Object.Destroy(node.gameObject);
                }
            }
            shownNodes.Remove(id);
        }
        if (diveReel.nodes != null)
        {
            foreach (DiveReelNode node in diveReel.nodes)
            {
                if (node)
                {
                    Object.Destroy(node.gameObject);
                }
            }
            diveReel.nodes.Clear();
        }
        diveReel.lastNodeTransform = null;
    }
}
