using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Core;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

[Serializable]
public class MapRoomCameraControl : Packet
{
	public NitroxId CameraId { get; }

	public Optional<NitroxId> MapRoomId { get; }

	public int CameraIndex { get; }

	public bool IsControlling { get; }

	public bool LightOn { get; }
	public bool IsServerResponse { get; }
	public bool Granted { get; }
	public SessionId ControllerSessionId { get; }

	public MapRoomCameraControl(NitroxId cameraId, Optional<NitroxId> mapRoomId, int cameraIndex, bool isControlling, bool lightOn, bool isServerResponse = false, bool granted = false, SessionId controllerSessionId = default)
	{
		CameraId = cameraId;
		MapRoomId = mapRoomId;
		CameraIndex = cameraIndex;
		IsControlling = isControlling;
		LightOn = lightOn;
		IsServerResponse = isServerResponse;
		Granted = granted;
		ControllerSessionId = controllerSessionId;
	}

	public override string ToString()
	{
		return $"[MapRoomCameraControl - CameraId: {CameraId}, MapRoomId: {MapRoomId}, CameraIndex: {CameraIndex}, IsControlling: {IsControlling}, LightOn: {LightOn}, IsServerResponse: {IsServerResponse}, Granted: {Granted}, ControllerSessionId: {ControllerSessionId}]";
	}
}




