using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Metadata;
using Nitrox.Model.Subnautica.Packets;
using NitroxClient.Communication;
using NitroxClient.GameLogic.FMOD;
using NitroxClient.GameLogic.Spawning.Metadata.Processor.Abstract;
using UnityEngine;

namespace NitroxClient.GameLogic.Spawning.Metadata.Processor;

public class MedicalCabinetMetadataProcessor : EntityMetadataProcessor<MedicalCabinetMetadata>
{
    /// <summary>
    ///     How long after its medkit is taken the game closes a cabinet's door (MedicalCabinet.OnHandClick).
    /// </summary>
    private const float DOOR_CLOSE_DELAY_AFTER_PICKUP = 2f;

    internal enum DoorChange
    {
        NONE,
        CANCEL_PENDING_TOGGLE,
        TOGGLE_NOW,
        TOGGLE_AFTER_PICKUP
    }

    public override void ProcessMetadata(GameObject gameObject, MedicalCabinetMetadata metadata)
    {
        if (!gameObject.TryGetComponent(out MedicalCabinet cabinet))
        {
            Log.Error($"[{nameof(MedicalCabinetMetadataProcessor)}] Could not find a MedicalCabinet on {gameObject.name}");
            return;
        }

        DoorChange doorChange = GetDoorChange(cabinet.doorOpen, cabinet.IsInvoking(nameof(MedicalCabinet.ToggleDoorState)), cabinet.hasMedKit, metadata);

        // MedicalCabinet.Update shows or hides the medkit and stops the blinking light when there is none. It only
        // starts the light for a medkit it makes itself.
        if (metadata.HasMedKit && !cabinet.IsInvoking(nameof(MedicalCabinet.BlinkRepeat)))
        {
            cabinet.InvokeRepeating(nameof(MedicalCabinet.BlinkRepeat), 0f, 1f);
        }
        cabinet.hasMedKit = metadata.HasMedKit;
        // Set before MedicalCabinet.Start has run, it also keeps Start from starting a timer of its own.
        cabinet.timeSpawnMedKit = metadata.TimeSpawnMedKit;

        // The player who opened or closed the door already plays its sound for everyone.
        using PacketSuppressor<FMODCustomEmitterPacket> packetSuppressor = PacketSuppressor<FMODCustomEmitterPacket>.Suppress();
        using FMODSoundSuppressor soundSuppressor = FMODSystem.SuppressSubnauticaSounds();
        switch (doorChange)
        {
            case DoorChange.CANCEL_PENDING_TOGGLE:
                cabinet.CancelInvoke(nameof(MedicalCabinet.ToggleDoorState));
                DelayedSilentReplay.Unmark(cabinet);
                break;
            case DoorChange.TOGGLE_NOW:
                cabinet.ToggleDoorState();
                break;
            case DoorChange.TOGGLE_AFTER_PICKUP:
                // It runs after this scope has ended; the mark keeps it as silent
                DelayedSilentReplay.Mark(cabinet);
                cabinet.Invoke(nameof(MedicalCabinet.ToggleDoorState), DOOR_CLOSE_DELAY_AFTER_PICKUP);
                break;
        }
    }

    /// <summary>
    ///     What to do with a cabinet's door so that it ends up as <paramref name="target" /> says.
    /// </summary>
    /// <param name="doorOpen">Whether the door is open now.</param>
    /// <param name="togglePending">Whether the game will open or close the door shortly (it does after a medkit is taken).</param>
    /// <param name="hadMedKit">Whether the cabinet held a medkit before this update.</param>
    internal static DoorChange GetDoorChange(bool doorOpen, bool togglePending, bool hadMedKit, MedicalCabinetMetadata target)
    {
        bool doorWillBeOpen = togglePending ? !doorOpen : doorOpen;
        if (doorWillBeOpen == target.DoorOpen)
        {
            return DoorChange.NONE;
        }
        if (togglePending)
        {
            // Without the pending toggle, the door stays as it is now, which is what the update wants.
            return DoorChange.CANCEL_PENDING_TOGGLE;
        }
        // Another player took the medkit: the door closes as it does for that player, not at once.
        if (doorOpen && hadMedKit && !target.HasMedKit)
        {
            return DoorChange.TOGGLE_AFTER_PICKUP;
        }
        return DoorChange.TOGGLE_NOW;
    }
}
