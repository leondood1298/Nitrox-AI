using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;
using UnityEngine;

namespace NitroxClient.GameLogic;

/// <summary>
///     A trash can destroys what is put in it after a while, in every game that has it loaded. One game tells the server:
///     the one whose player put the item in, and the one that simulates the trash can or its base. Before, only the latter
///     did, and with nobody simulating them the item stayed on the server and came back for a joining player and after a
///     restart.
/// </summary>
public class Trashcans
{
    private readonly IPacketSender packetSender;
    private readonly SimulationOwnership simulationOwnership;

    /// <summary>
    ///     The items this game's player put into a trash can.
    /// </summary>
    private readonly HashSet<NitroxId> putInByThisPlayer = [];

    public Trashcans(IPacketSender packetSender, SimulationOwnership simulationOwnership)
    {
        this.packetSender = packetSender;
        this.simulationOwnership = simulationOwnership;
    }

    public void ItemPutIn(NitroxId itemId)
    {
        putInByThisPlayer.Add(itemId);
    }

    /// <summary>
    ///     Called when a trash can destroys an item.
    /// </summary>
    public void Destroyed(Trashcan trashcan, GameObject item)
    {
        if (!item.TryGetNitroxId(out NitroxId itemId))
        {
            return;
        }
        if (ShouldReport(putInByThisPlayer.Remove(itemId), SimulatesTrashcan(trashcan)))
        {
            packetSender.Send(new EntityDestroyed(itemId));
        }
    }

    internal static bool ShouldReport(bool putInByThisPlayer, bool simulatesTrashcan) => putInByThisPlayer || simulatesTrashcan;

    /// <remarks>
    ///     Players only get simulation ownership on the base and on some objects: under a SubRoot that of the SubRoot counts,
    ///     else that of the trash can.
    /// </remarks>
    private bool SimulatesTrashcan(Trashcan trashcan)
    {
        if (trashcan.transform.parent && trashcan.transform.parent.GetComponent<SubRoot>())
        {
            return trashcan.transform.parent.TryGetNitroxId(out NitroxId subRootId) && simulationOwnership.HasAnyLockType(subRootId);
        }
        return trashcan.TryGetNitroxId(out NitroxId trashcanId) && simulationOwnership.HasAnyLockType(trashcanId);
    }
}
