using System.Threading.Tasks;
using Nitrox.Model.Packets.Core;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Model.Core;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomCameraControlProcessor(SimulationOwnershipData simulationOwnershipData, EntityRegistry entityRegistry,
	MapRoomCameraControlLifecycle controlLifecycle, ScannerRoomDiagnostics diagnostics) : IAuthPacketProcessor<MapRoomCameraControl>, IAuthPacketProcessor, IPacketProcessor, IPacketProcessor<AuthProcessorContext, MapRoomCameraControl>
{
	private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;
	private readonly EntityRegistry entityRegistry = entityRegistry;
	private readonly MapRoomCameraControlLifecycle controlLifecycle = controlLifecycle;
	private readonly ScannerRoomDiagnostics diagnostics = diagnostics;

	public async Task Process(AuthProcessorContext context, MapRoomCameraControl packet)
	{
		if (packet.IsServerResponse || !IsValidAssociation(packet))
		{
			diagnostics.RecordRejected("control", GetRoom(packet.MapRoomId), packet.CameraId, context.Sender.SessionId, packet.CameraIndex,
				packet.IsServerResponse ? "server_response" : "invalid_assoc");
			await context.ReplyAsync(CreateResponse(packet, false, context.Sender.SessionId));
			return;
		}
		using IDisposable lifecycleGate = await controlLifecycle.EnterAsync(packet.CameraId);
		if (!IsValidAssociation(packet))
		{
			diagnostics.RecordRejected("control", GetRoom(packet.MapRoomId), packet.CameraId, context.Sender.SessionId,
				packet.CameraIndex, "association_changed");
			await context.ReplyAsync(CreateResponse(packet, false, context.Sender.SessionId));
			return;
		}

		if (packet.IsControlling)
		{
			bool granted = simulationOwnershipData.TryToAcquire(packet.CameraId, context.Sender, SimulationLockType.EXCLUSIVE);
			SessionId controller = granted ? context.Sender.SessionId : simulationOwnershipData.GetPlayerForLock(packet.CameraId)?.SessionId ?? context.Sender.SessionId;
			MapRoomCameraControl response = CreateResponse(packet, granted, controller);
			if (granted)
			{
				controlLifecycle.BeginPreviewAcquisition(packet.CameraId, context.Sender.SessionId);
				diagnostics.RecordAccepted("control_acquire", GetRoom(packet.MapRoomId), packet.CameraId, context.Sender.SessionId, packet.CameraIndex);
				await context.SendToAllAsync(response);
			}
			else
			{
				diagnostics.RecordRejected("control_acquire", GetRoom(packet.MapRoomId), packet.CameraId, context.Sender.SessionId, packet.CameraIndex, "locked");
				await context.ReplyAsync(response);
			}
			return;
		}

		Player? currentOwner = simulationOwnershipData.GetPlayerForLock(packet.CameraId);
		if (CanAcknowledgeRelease(currentOwner != null, currentOwner == context.Sender))
		{
			controlLifecycle.EndPreviewAcquisition(packet.CameraId, context.Sender.SessionId);
			if (currentOwner != null)
			{
				simulationOwnershipData.RevokeIfOwner(packet.CameraId, context.Sender);
			}
			diagnostics.RecordAccepted("control_release", GetRoom(packet.MapRoomId), packet.CameraId, context.Sender.SessionId, packet.CameraIndex,
				currentOwner == null ? "idempotent" : null);
			await context.SendToAllAsync(CreateResponse(packet, true, context.Sender.SessionId));
		}
		else
		{
			diagnostics.RecordRejected("control_release", GetRoom(packet.MapRoomId), packet.CameraId, context.Sender.SessionId, packet.CameraIndex, "non_owner");
			await context.ReplyAsync(CreateResponse(packet, false, simulationOwnershipData.GetPlayerForLock(packet.CameraId)?.SessionId ?? context.Sender.SessionId));
		}
	}

	internal static bool CanAcknowledgeRelease(bool hasOwner, bool senderOwnsLock) => !hasOwner || senderOwnsLock;

	private bool IsValidAssociation(MapRoomCameraControl packet)
	{
		if (!packet.IsControlling)
		{
			return IsKnownScannerCamera(packet.CameraId);
		}
		if (packet.MapRoomId.HasValue)
		{
			if (packet.CameraIndex is < 0 or > 1 || !entityRegistry.TryGetEntityById(packet.MapRoomId.Value, out MapRoomEntity mapRoom))
			{
				return false;
			}
			lock (mapRoom)
			{
				return mapRoom.GetCameraRecord(packet.CameraId) != null && mapRoom.GetDockedCamera(packet.CameraIndex) == packet.CameraId;
			}
		}

		int registrations = 0;
		bool isDocked = false;
		foreach (MapRoomEntity room in entityRegistry.GetEntities<MapRoomEntity>())
		{
			lock (room)
			{
				if (room.GetCameraRecord(packet.CameraId) != null)
				{
					registrations++;
					isDocked |= room.IsCameraDocked(packet.CameraId);
				}
			}
		}
		bool validWorldCamera = entityRegistry.TryGetEntityById(packet.CameraId, out WorldEntity worldCamera) &&
			new NitroxTechType("MapRoomCamera").Equals(worldCamera.TechType);
		return IsValidLooseAssociation(validWorldCamera, registrations, isDocked);
	}

	internal static bool IsValidLooseAssociation(bool validWorldCamera, int registrations, bool isDocked) =>
		registrations == 0 ? validWorldCamera : registrations == 1 && !isDocked;

	private bool IsKnownScannerCamera(NitroxId cameraId)
	{
		if (entityRegistry.TryGetEntityById(cameraId, out WorldEntity worldCamera) &&
			new NitroxTechType("MapRoomCamera").Equals(worldCamera.TechType))
		{
			return true;
		}
		foreach (MapRoomEntity room in entityRegistry.GetEntities<MapRoomEntity>())
		{
			lock (room)
			{
				if (room.GetCameraRecord(cameraId) != null)
				{
					return true;
				}
			}
		}
		return false;
	}

	private MapRoomEntity? GetRoom(Optional<NitroxId> roomId) =>
		roomId.HasValue && entityRegistry.TryGetEntityById(roomId.Value, out MapRoomEntity room) ? room : null;

	private static MapRoomCameraControl CreateResponse(MapRoomCameraControl packet, bool granted, SessionId controller) =>
		new(packet.CameraId, packet.MapRoomId, packet.CameraIndex, packet.IsControlling, packet.LightOn, true, granted, controller);
}


