using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

[Serializable]
public class MapRoomCameraLight : Packet
{
	public NitroxId CameraId { get; }

	public bool On { get; }
	public long Revision { get; }
	public bool IsServerResponse { get; }
	public bool Granted { get; }

	public MapRoomCameraLight(NitroxId cameraId, bool on, long revision = 0, bool isServerResponse = false, bool granted = false)
	{
		CameraId = cameraId;
		On = on;
		Revision = revision;
		IsServerResponse = isServerResponse;
		Granted = granted;
	}

	public override string ToString()
	{
		return $"[MapRoomCameraLight - CameraId: {CameraId}, On: {On}, Revision: {Revision}, IsServerResponse: {IsServerResponse}, Granted: {Granted}]";
	}
}




