using System;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Packets;

namespace Nitrox.Model.Subnautica.Packets;

[Serializable]
public class MapRoomCameraComponentState : Packet
{
    public NitroxId CameraId { get; }
    public float Energy { get; }
    public float Health { get; }
    public long Revision { get; }
    public bool IsServerResponse { get; }
    public bool Granted { get; }

    public MapRoomCameraComponentState(NitroxId cameraId, float energy, float health, long revision = 0, bool isServerResponse = false, bool granted = false)
    {
        CameraId = cameraId;
        Energy = energy;
        Health = health;
        Revision = revision;
        IsServerResponse = isServerResponse;
        Granted = granted;
    }
}
