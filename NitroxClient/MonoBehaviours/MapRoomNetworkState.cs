using UnityEngine;

namespace NitroxClient.MonoBehaviours;

public sealed class MapRoomNetworkState : MonoBehaviour
{
    public long Generation { get; set; }

    public long Revision { get; set; }
}
