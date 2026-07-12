using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

[Serializable]
public class MapRoomCameraDock : Packet
{
	public NitroxId CameraId { get; }

	public NitroxId MapRoomId { get; }

	public int DockingIndex { get; }
	public long Revision { get; }
	public bool IsServerResponse { get; }
	public bool Granted { get; }
	public bool IsDocked { get; }
	public int CameraNumber { get; }
	public bool LightOn { get; }
	public long LightRevision { get; }

	public MapRoomCameraDock(NitroxId cameraId, NitroxId mapRoomId, int dockingIndex, long revision = 0, bool isServerResponse = false, bool granted = false, bool isDocked = true, int cameraNumber = 0, bool lightOn = false, long lightRevision = 0)
	{
		CameraId = cameraId;
		MapRoomId = mapRoomId;
		DockingIndex = dockingIndex;
		Revision = revision;
		IsServerResponse = isServerResponse;
		Granted = granted;
		IsDocked = isDocked;
		CameraNumber = cameraNumber;
		LightOn = lightOn;
		LightRevision = lightRevision;
	}

	public override string ToString()
	{
		return $"[MapRoomCameraDock - CameraId: {CameraId}, MapRoomId: {MapRoomId}, DockingIndex: {DockingIndex}, Revision: {Revision}, IsServerResponse: {IsServerResponse}, Granted: {Granted}, IsDocked: {IsDocked}, CameraNumber: {CameraNumber}]";
	}
}




