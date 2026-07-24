using NitroxClient.Communication.Abstract;
using NitroxClient.GameLogic;
using NitroxClient.MonoBehaviours.Cyclops;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;
using Nitrox.Model.Subnautica.DataStructures;
using Nitrox.Model.Subnautica.Packets;
using UnityEngine;

namespace NitroxClient.MonoBehaviours;

public class PlayerMovementBroadcaster : MonoBehaviour
{
    private LocalPlayer localPlayer;
    private ScannerRoomClientDiagnostics scannerRoomDiagnostics;
    private bool scannerAnchorActive;
    private MapRoomCamera scannerAnchorCamera;
    private NitroxId? scannerAnchorCameraId;

    public void Awake()
    {
        localPlayer = this.Resolve<LocalPlayer>();
        scannerRoomDiagnostics = this.Resolve<ScannerRoomClientDiagnostics>();
    }

    public void Update()
    {
        // TODO: Replace this temporary fix. Mostly prevents server console being spammed with warnings when a client is in the queue.
        // There should be a way to block all packets from being sent when in the join queue or during initial sync.
        if (!Multiplayer.Main.InitialSyncCompleted)
        {
            return;
        }

        MapRoomCamera scannerCamera = uGUI_CameraDrone.main ? uGUI_CameraDrone.main.GetCamera() : null;
        bool scannerCameraActive = scannerCamera;
        RecordScannerAnchorTransition(scannerCamera);
        PlayerMovementBroadcastMode broadcastMode = GetBroadcastMode(MainCameraControl.main.isActiveAndEnabled,
            uGUI_CameraCyclops.main.content.activeSelf, scannerCameraActive);
        if (broadcastMode == PlayerMovementBroadcastMode.SUPPRESSED)
        {
            return;
        }

        if (broadcastMode != PlayerMovementBroadcastMode.SCANNER_CAMERA_ANCHOR && BroadcastPlayerInCyclopsMovement())
        {
            return;
        }

        if (broadcastMode != PlayerMovementBroadcastMode.SCANNER_CAMERA_ANCHOR && Player.main.isPiloting)
        {
            return;
        }

        BroadcastPlayerMovement(broadcastMode == PlayerMovementBroadcastMode.SCANNER_CAMERA_ANCHOR);
    }

    private void BroadcastPlayerMovement(bool pinAtScannerConsole)
    {
        Vector3 currentPosition = Player.main.transform.position;
        Vector3 playerVelocity = GetBroadcastVelocity(pinAtScannerConsole, Player.main.playerController.velocity);

        // Scanner control moves SNCameraRoot to the drone, not the player. Keep the remote avatar
        // at the physical console instead of publishing the drone's view rotation.
        Quaternion bodyRotation = pinAtScannerConsole
            ? Player.main.transform.rotation
            : MainCameraControl.main.viewModel.transform.rotation;
        Quaternion aimingRotation = pinAtScannerConsole
            ? bodyRotation
            : Player.main.camRoot.GetAimingTransform().rotation;

        SubRoot subRoot = Player.main.GetCurrentSub();

        // If in a subroot the position will be relative to the subroot
        if (subRoot)
        {
            // Rotate relative player position relative to the subroot (else there are problems with respawning)
            Transform subRootTransform = subRoot.transform;
            Quaternion undoVehicleAngle = subRootTransform.rotation.GetInverse();
            currentPosition = currentPosition - subRootTransform.position;
            currentPosition = undoVehicleAngle * currentPosition;
            bodyRotation = undoVehicleAngle * bodyRotation;
            aimingRotation = undoVehicleAngle * aimingRotation;
            currentPosition = subRootTransform.TransformPoint(currentPosition);
        }

        localPlayer.BroadcastLocation(currentPosition, playerVelocity, bodyRotation, aimingRotation);
    }

    internal static PlayerMovementBroadcastMode GetBroadcastMode(bool mainCameraControlEnabled,
        bool cyclopsCameraActive, bool scannerCameraActive)
    {
        if (scannerCameraActive)
        {
            return PlayerMovementBroadcastMode.SCANNER_CAMERA_ANCHOR;
        }
        return mainCameraControlEnabled || cyclopsCameraActive
            ? PlayerMovementBroadcastMode.NORMAL
            : PlayerMovementBroadcastMode.SUPPRESSED;
    }

    internal static Vector3 GetBroadcastVelocity(bool pinAtScannerConsole, Vector3 playerVelocity) =>
        pinAtScannerConsole ? Vector3.zero : playerVelocity;

    private void RecordScannerAnchorTransition(MapRoomCamera scannerCamera)
    {
        bool active = scannerCamera;
        NitroxId? cameraId = active && scannerCamera.TryGetNitroxId(out NitroxId resolvedCameraId)
            ? resolvedCameraId
            : null;
        ScannerAnchorTransition transition = GetScannerAnchorTransition(scannerAnchorActive, active,
            scannerAnchorCamera != scannerCamera, !Equals(scannerAnchorCameraId, cameraId));
        if (transition == ScannerAnchorTransition.NONE)
        {
            return;
        }

        NitroxId? diagnosticCameraId = transition == ScannerAnchorTransition.EXIT
            ? scannerAnchorCameraId
            : cameraId;
        scannerAnchorActive = active;
        scannerAnchorCamera = scannerCamera;
        scannerAnchorCameraId = cameraId;
        scannerRoomDiagnostics.Record("player_body_pin", "ok", diagnosticCameraId, reason: transition switch
        {
            ScannerAnchorTransition.ENTER => "enter_console",
            ScannerAnchorTransition.SWITCH => "switch_console",
            ScannerAnchorTransition.IDENTIFIED => "identify_console",
            _ => "exit_console"
        });
    }

    internal static ScannerAnchorTransition GetScannerAnchorTransition(bool wasActive, bool active,
        bool cameraChanged, bool idChanged)
    {
        if (!wasActive)
        {
            return active ? ScannerAnchorTransition.ENTER : ScannerAnchorTransition.NONE;
        }
        if (!active)
        {
            return ScannerAnchorTransition.EXIT;
        }
        if (cameraChanged)
        {
            return ScannerAnchorTransition.SWITCH;
        }
        return idChanged ? ScannerAnchorTransition.IDENTIFIED : ScannerAnchorTransition.NONE;
    }

    private bool BroadcastPlayerInCyclopsMovement()
    {
        if (!Player.main.isPiloting && Player.main.TryGetComponent(out CyclopsMotor cyclopsMotor) && cyclopsMotor.Pawn != null)
        {
            Transform pawnTransform = cyclopsMotor.Pawn.Handle.transform;
            PlayerInCyclopsMovement packet = new(this.Resolve<LocalPlayer>().SessionId.Value, pawnTransform.localPosition.ToDto(), pawnTransform.localRotation.ToDto());
            this.Resolve<IPacketSender>().Send(packet);
            return true;
        }
        return false;
    }
}

internal enum PlayerMovementBroadcastMode
{
    SUPPRESSED,
    NORMAL,
    SCANNER_CAMERA_ANCHOR
}

internal enum ScannerAnchorTransition
{
    NONE,
    ENTER,
    SWITCH,
    IDENTIFIED,
    EXIT
}
