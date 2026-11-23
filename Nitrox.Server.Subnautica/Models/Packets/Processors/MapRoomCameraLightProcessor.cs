using System.Threading.Tasks;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets.Core;
using Nitrox.Model.Subnautica.Packets;
using Nitrox.Server.Subnautica.Models.GameLogic;
using Nitrox.Server.Subnautica.Models.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Server.Subnautica.Models.Packets.Core;

namespace Nitrox.Server.Subnautica.Models.Packets.Processors;

internal sealed class MapRoomCameraLightProcessor(SimulationOwnershipData simulationOwnershipData,
	EntityRegistry entityRegistry, MapRoomCameraControlLifecycle controlLifecycle,
	ScannerRoomDiagnostics diagnostics) : IAuthPacketProcessor<MapRoomCameraLight>, IAuthPacketProcessor, IPacketProcessor, IPacketProcessor<AuthProcessorContext, MapRoomCameraLight>
{
	private readonly SimulationOwnershipData simulationOwnershipData = simulationOwnershipData;
	private readonly EntityRegistry entityRegistry = entityRegistry;
	private readonly MapRoomCameraControlLifecycle controlLifecycle = controlLifecycle;
	private readonly ScannerRoomDiagnostics diagnostics = diagnostics;

	public async Task Process(AuthProcessorContext context, MapRoomCameraLight packet)
	{
		MapRoomEntity? mapRoom = FindUniqueRoom(packet.CameraId);
		if (packet.IsServerResponse || mapRoom == null)
		{
			diagnostics.RecordRejected("light", mapRoom, packet.CameraId, context.Sender.SessionId, reason:
				packet.IsServerResponse ? "server_response" : "invalid_assoc");
			await context.ReplyAsync(new MapRoomCameraLight(packet.CameraId, packet.On, 0, true, false));
			return;
		}

		long revision = 0;
		bool changed = false;
		bool accepted = false;
		string rejectionReason = "association_changed";
		Task sendTask = Task.CompletedTask;
		lock (mapRoom)
		{
			MapRoomCameraRecord? record = mapRoom.GetCameraRecord(packet.CameraId);
			sendTask = simulationOwnershipData.ExecuteForOwner(context.Sender, [packet.CameraId], ownedIds =>
			{
				bool hasCanonicalControl =
					simulationOwnershipData.TryGetLock(packet.CameraId,
						out SimulationOwnershipData.PlayerLock cameraLock) &&
					cameraLock.Player == context.Sender &&
					cameraLock.LockType == SimulationLockType.EXCLUSIVE &&
					controlLifecycle.IsActiveController(packet.CameraId, context.Sender.SessionId);
				if (record == null || !ownedIds.Contains(packet.CameraId) || !hasCanonicalControl)
				{
					rejectionReason = record == null ? "association_changed" :
						!ownedIds.Contains(packet.CameraId) ? "non_owner" : "control_required";
					return Task.CompletedTask;
				}

				lock (record)
				{
					accepted = true;
					changed = record.LightOn != packet.On;
					revision = record.LightRevision;
					if (changed)
					{
						revision++;
						record.LightOn = packet.On;
						record.LightRevision = revision;
					}
				}

				// LiteNetLib enqueues synchronously. Queue the accepted state while ownership is
				// locked so a release or reassignment packet cannot overtake this transition.
				return context.SendToAllAsync(new MapRoomCameraLight(packet.CameraId, packet.On, revision, true, true));
			});
		}
		if (!accepted)
		{
			diagnostics.RecordRejected("light", mapRoom, packet.CameraId, context.Sender.SessionId,
				reason: rejectionReason);
			await context.ReplyAsync(new MapRoomCameraLight(packet.CameraId, packet.On, 0, true, false));
			return;
		}
		if (changed)
		{
			diagnostics.RecordAccepted("light", mapRoom, packet.CameraId, context.Sender.SessionId,
				reason: packet.On ? "on" : "off");
		}
		await sendTask;
	}

	private MapRoomEntity? FindUniqueRoom(Nitrox.Model.DataStructures.NitroxId cameraId)
	{
		MapRoomEntity? found = null;
		foreach (MapRoomEntity room in entityRegistry.GetEntities<MapRoomEntity>())
		{
			lock (room)
			{
				if (room.GetCameraRecord(cameraId) == null)
				{
					continue;
				}
				if (found != null)
				{
					return null;
				}
				found = room;
			}
		}
		return found;
	}
}

