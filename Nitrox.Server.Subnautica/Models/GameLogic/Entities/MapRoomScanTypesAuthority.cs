using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;
using Nitrox.Model.Subnautica.Packets;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

internal static class MapRoomScanTypesAuthority
{
    private const int MAX_SCAN_TYPES = 512;

    public static bool TryApply(MapRoomEntity room, MapRoomScanTypesSnapshot requested, IEnumerable<WorldEntity> worldEntities, NitroxVector3 scanAnchor,
        out List<NitroxTechType> accepted, out long revision)
    {
        lock (room)
        {
            accepted = [];
            revision = room.AvailableScanTypesRevision;
            if (requested.IsServerResponse || requested.MapRoomId != room.Id || requested.TechTypes == null || requested.TechTypes.Count > MAX_SCAN_TYPES ||
                requested.DetectableTechTypes == null || requested.DetectableTechTypes.Count > MAX_SCAN_TYPES || worldEntities == null ||
                !MapRoomWorldResourceIndex.TryNormalizeQuery(requested.ScanOrigin, requested.ScanRange, scanAnchor,
                    out NitroxVector3 scanOrigin, out float scanRange))
            {
                return false;
            }
            if (!MapRoomWorldResourceIndex.IsRangeAllowed(room, scanRange))
            {
                return false;
            }
            HashSet<NitroxTechType> unique = [];
            foreach (NitroxTechType techType in requested.TechTypes)
            {
                if (techType == null || techType.Equals(NitroxTechType.None) || !unique.Add(techType))
                {
                    return false;
                }
            }
            HashSet<NitroxTechType> detectable = [];
            foreach (NitroxTechType techType in requested.DetectableTechTypes)
            {
                if (techType == null || techType.Equals(NitroxTechType.None) || !detectable.Add(techType))
                {
                    return false;
                }
            }
            List<NitroxTechType> normalized = MapRoomWorldResourceIndex.MergeScanTypes(unique, detectable, worldEntities,
                scanOrigin, scanRange);
            if (room.AvailableScanTypesRevision > 0 && room.AvailableScanTypes.SequenceEqual(normalized))
            {
                accepted = room.AvailableScanTypes.ToList();
                return true;
            }
            room.AvailableScanTypes = normalized;
            room.AvailableScanTypesRevision++;
            revision = room.AvailableScanTypesRevision;
            accepted = room.AvailableScanTypes.ToList();
            return true;
        }
    }
}
