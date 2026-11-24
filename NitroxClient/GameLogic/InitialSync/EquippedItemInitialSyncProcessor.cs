using System.Collections;
using System.Collections.Generic;
using NitroxClient.GameLogic.Helper;
using NitroxClient.GameLogic.InitialSync.Abstract;
using NitroxClient.MonoBehaviours;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using UnityEngine;

namespace NitroxClient.GameLogic.InitialSync;

public sealed class EquippedItemInitialSyncProcessor : InitialSyncProcessor
{
    public EquippedItemInitialSyncProcessor()
    {
        AddDependency<PlayerInitialSyncProcessor>();
        AddDependency<RemotePlayerInitialSyncProcessor>();
        AddDependency<GlobalRootInitialSyncProcessor>();
    }

    public override IEnumerator Process(InitialPlayerSync packet, WaitScreen.ManualWaitItem waitScreenItem)
    {
        int totalEquippedItemsDone = 0;

        foreach (KeyValuePair<string, NitroxId> equippedItem in packet.EquippedItems)
        {
            string slot = equippedItem.Key;
            NitroxId id = equippedItem.Value;

            waitScreenItem.SetProgress(totalEquippedItemsDone, packet.EquippedItems.Count);

            GameObject gameObject = NitroxEntity.RequireObjectFrom(id);
            Pickupable pickupable = gameObject.RequireComponent<Pickupable>();

            Optional<Equipment> opEquipment = EquipmentHelper.FindEquipmentComponent(Player.mainObject);
            if (opEquipment.HasValue)
            {
                // The body's metadata may have put it on already when the body was spawned
                EquipmentSlots.PutOnLocalPlayer(opEquipment.Value, pickupable, slot);
            }
            else
            {
                Log.Info($"Could not find equipment type for {gameObject.name}");
            }

            totalEquippedItemsDone++;
            yield return null;
        }

        Log.Info($"Recieved initial sync with {totalEquippedItemsDone} pieces of equipped items");
    }
}
