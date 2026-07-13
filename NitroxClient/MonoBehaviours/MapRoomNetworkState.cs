using System.Collections.Generic;
using UnityEngine;

namespace NitroxClient.MonoBehaviours;

public sealed class MapRoomNetworkState : MonoBehaviour
{
    public bool MetadataInitialized { get; set; }

    public long Generation { get; set; }

    public long Revision { get; set; }

    public long ResultGeneration { get; set; }

    public long ResultRevision { get; set; }

    public long AvailableScanTypesRevision { get; set; }

    public bool AvailableScanTypesInitialized { get; set; }

    public HashSet<TechType> AvailableScanTypes { get; } = [];
}
