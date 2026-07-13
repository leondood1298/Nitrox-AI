using System;
using System.Collections.Generic;
using System.Linq;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.DataStructures.GameLogic.Entities.Bases;

namespace Nitrox.Server.Subnautica.Models.GameLogic.Entities;

internal static class MapRoomCameraPersistence
{
    public static int RestoreOrphanedRegistrations(MapRoomEntity mapRoom, Func<NitroxId, bool> entityExists)
    {
        int restored = 0;
        List<MapRoomCameraRecord> records;
        lock (mapRoom)
        {
            records = mapRoom.CameraRegistry.OrderBy(record => record.CameraNumber).ToList();
        }
        foreach (MapRoomCameraRecord record in records)
        {
            // Query the registry outside the room lock to preserve the registry -> room lock order used by packet processors.
            if (entityExists(record.CameraId))
            {
                continue;
            }
            lock (mapRoom)
            {
                if (mapRoom.IsCameraDocked(record.CameraId))
                {
                    continue;
                }

                int preferredSlot = record.CameraNumber - 1;
                int slot = preferredSlot is >= 0 and <= 1 && mapRoom.GetDockedCamera(preferredSlot) == null
                    ? preferredSlot
                    : mapRoom.LeftDockCameraId == null ? 0
                    : mapRoom.RightDockCameraId == null ? 1
                    : -1;
                if (slot < 0)
                {
                    break;
                }

                mapRoom.SetDockedCamera(slot, record.CameraId);
                restored++;
            }
        }
        return restored;
    }
}
