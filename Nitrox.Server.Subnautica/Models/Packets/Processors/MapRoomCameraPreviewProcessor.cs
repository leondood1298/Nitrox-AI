using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets.Core;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Helper;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomCameraPreviewProcessor(SimulationOwnershipData simulationOwnershipData,
    EntityRegistry entityRegistry, MapRoomCameraControlLifecycle controlLifecycle,
    ScannerRoomDiagnostics diagnostics) : IAuthPacketProcessor<MapRoomCameraPreview>
{
    private static readonly NitroxTechType mapRoomCameraTechType = new("MapRoomCamera");

    public async Task Process(AuthProcessorContext context, MapRoomCameraPreview packet)
    {
        if (packet.IsServerResponse)
        {
            diagnostics.RecordRejected("preview", cameraId: packet.CameraId, sessionId: context.Sender.SessionId,
                reason: "server_response");
            await context.ReplyAsync(CreateDenied(packet.CameraId, 0));
            return;
        }

        using IDisposable lifecycleGate = await controlLifecycle.EnterAsync(packet.CameraId);
        bool validAssociation = TryResolveCameraNumber(packet.CameraId, packet.CameraNumber,
            out MapRoomEntity? room, out int cameraNumber);
        bool accepted = false;
        int width = 0;
        int height = 0;
        long revision = 0;
        string rejectionReason = "non_owner";
        Task sendTask = Task.CompletedTask;
        simulationOwnershipData.ExecuteForOwner(context.Sender, [packet.CameraId], ownedIds =>
        {
            bool hasExclusiveLock = simulationOwnershipData.TryGetLock(packet.CameraId,
                out SimulationOwnershipData.PlayerLock playerLock) &&
                playerLock.Player == context.Sender && playerLock.LockType == SimulationLockType.EXCLUSIVE;
            if (!ownedIds.Contains(packet.CameraId) || !hasExclusiveLock)
            {
                return false;
            }
            if (!controlLifecycle.TryConsumePreviewAcquisition(packet.CameraId, context.Sender.SessionId))
            {
                rejectionReason = "already_published";
                return false;
            }

            // Consume the single acquisition opportunity even for malformed data. A controller cannot use
            // an invalid payload to turn this presentation packet into an unbounded retry channel.
            if (!validAssociation)
            {
                rejectionReason = "invalid_assoc";
                return false;
            }
            if (!MapRoomCameraPreviewImage.TryValidate(packet.JpegBytes, out width, out height))
            {
                rejectionReason = "invalid_jpeg";
                return false;
            }

            accepted = true;
            revision = controlLifecycle.NextPreviewRevision();
            sendTask = context.SendToAllAsync(new MapRoomCameraPreview(packet.CameraId, cameraNumber,
                packet.JpegBytes, true, true, revision));
            return true;
        });

        if (!accepted)
        {
            diagnostics.RecordRejected("preview", room, packet.CameraId, context.Sender.SessionId, cameraNumber,
                $"{rejectionReason}_bytes_{packet.JpegBytes?.Length ?? 0}_{width}x{height}");
            await context.ReplyAsync(CreateDenied(packet.CameraId, cameraNumber));
            return;
        }
        diagnostics.RecordAccepted("preview", room, packet.CameraId, context.Sender.SessionId, cameraNumber,
            $"rev_{revision}_bytes_{packet.JpegBytes.Length}_{width}x{height}");
        await sendTask;
    }

    private bool TryResolveCameraNumber(NitroxId cameraId, int requestedCameraNumber,
        out MapRoomEntity? room, out int cameraNumber)
    {
        room = null;
        cameraNumber = 0;
        int registrations = 0;
        MapRoomEntity? registeredRoom = null;
        foreach (MapRoomEntity candidateRoom in entityRegistry.GetEntities<MapRoomEntity>())
        {
            lock (candidateRoom)
            {
                if (candidateRoom.GetCameraRecord(cameraId) is not { } record)
                {
                    continue;
                }
                registrations++;
                if (registrations > 1)
                {
                    cameraNumber = 0;
                    return false;
                }
                cameraNumber = record.CameraNumber;
                registeredRoom = candidateRoom;
            }
        }

        if (registrations == 1)
        {
            room = registeredRoom;
            return cameraNumber > 0;
        }

        if (entityRegistry.TryGetEntityById(cameraId, out WorldEntity worldCamera) &&
            mapRoomCameraTechType.Equals(worldCamera.TechType) &&
            MapRoomCameraPreview.IsValidLooseCameraNumber(requestedCameraNumber))
        {
            cameraNumber = requestedCameraNumber;
            return true;
        }
        return false;
    }

    private static MapRoomCameraPreview CreateDenied(NitroxId cameraId, int cameraNumber) =>
        new(cameraId, cameraNumber, [], true, false, 0);
}
