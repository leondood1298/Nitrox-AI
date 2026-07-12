using UnityEngine;

namespace NitroxClient.MonoBehaviours;

public sealed class MapRoomNetworkState : MonoBehaviour
{
    public long Generation { get; set; }

    public long Revision { get; set; }

    public long ResultGeneration { get; set; }

    public long ResultRevision { get; set; }
}
