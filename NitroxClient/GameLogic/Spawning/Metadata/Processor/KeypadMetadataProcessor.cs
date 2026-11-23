using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class KeypadMetadataProcessor : EntityMetadataProcessor<KeypadMetadata>
{
    public override void ProcessMetadata(GameObject gameObject, KeypadMetadata metadata)
    {
        Log.Debug($"Received keypad metadata change for {gameObject.name} with data of {metadata}");

        KeypadDoorConsole keypad = gameObject.GetComponent<KeypadDoorConsole>();
        keypad.unlocked = metadata.Unlocked;

        if (metadata.Unlocked)
        {
            // The door patch would report the unlock with the door's current, possibly prefab, state. The door is its own
            // entity and the player who typed the code already sent its state.
            using (PacketSuppressor<EntityMetadataUpdate>.Suppress())
            {
                if (keypad.root)
                {
                    keypad.root.BroadcastMessage("UnlockDoor");
                }
                else
                {
                    keypad.BroadcastMessage("UnlockDoor");
                }
            }

            keypad.UnlockDoor();
        }
    }
}
