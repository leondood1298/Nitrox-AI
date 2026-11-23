using System.Collections.Generic;
using Nitrox.Model.DataStructures;
using Nitrox.Model.Subnautica.Packets;

namespace NitroxClient.GameLogic;

/// <summary>
/// Retains authoritative Scanner Room camera transitions that can arrive before a newly-built
/// room and its default camera prefabs have finished spawning on this client.
/// </summary>
internal sealed class MapRoomCameraBootstrapStateCache
{
    private readonly Dictionary<(NitroxId MapRoomId, int DockingIndex), MapRoomCameraDock> dockStates = new();
    private readonly HashSet<DockStateKey> appliedDockStates = [];
    private readonly Dictionary<NitroxId, MapRoomCameraControl> pendingControls = new();

    public bool RetainDock(MapRoomCameraDock packet)
    {
        if (!packet.IsServerResponse || !packet.Granted || packet.MapRoomId == null ||
            packet.DockingIndex is < 0 or > 1)
        {
            return false;
        }

        (NitroxId, int) key = (packet.MapRoomId, packet.DockingIndex);
        if (dockStates.TryGetValue(key, out MapRoomCameraDock existing))
        {
            if (existing.Revision > packet.Revision ||
                existing.Revision == packet.Revision &&
                (existing.CameraId != packet.CameraId || existing.IsDocked != packet.IsDocked))
            {
                return false;
            }
            if (existing.Revision == packet.Revision)
            {
                MapRoomCameraDock merged = MergeEqualRevision(existing, packet);
                if (HasSameCanonicalState(existing, merged))
                {
                    return false;
                }
                dockStates[key] = merged;
                return true;
            }
            appliedDockStates.Remove(DockStateKey.From(existing));
        }

        dockStates[key] = packet;
        return true;
    }

    public bool TryGetDock(NitroxId mapRoomId, int dockingIndex, out MapRoomCameraDock packet) =>
        dockStates.TryGetValue((mapRoomId, dockingIndex), out packet);

    public IReadOnlyList<MapRoomCameraDock> GetDocks(NitroxId mapRoomId)
    {
        List<MapRoomCameraDock> packets = [];
        for (int dockingIndex = 0; dockingIndex < 2; dockingIndex++)
        {
            if (TryGetDock(mapRoomId, dockingIndex, out MapRoomCameraDock packet))
            {
                packets.Add(packet);
            }
        }
        packets.Sort((left, right) =>
        {
            int revisionComparison = left.Revision.CompareTo(right.Revision);
            return revisionComparison != 0
                ? revisionComparison
                : left.DockingIndex.CompareTo(right.DockingIndex);
        });
        return packets;
    }

    public IReadOnlyList<MapRoomCameraDock> GetPendingDocks(NitroxId mapRoomId)
    {
        List<MapRoomCameraDock> packets = [];
        foreach (MapRoomCameraDock packet in GetDocks(mapRoomId))
        {
            if (!appliedDockStates.Contains(DockStateKey.From(packet)))
            {
                packets.Add(packet);
            }
        }
        return packets;
    }

    public void MarkDockApplied(MapRoomCameraDock packet) => appliedDockStates.Add(DockStateKey.From(packet));

    public void MarkDockPending(MapRoomCameraDock packet) => appliedDockStates.Remove(DockStateKey.From(packet));

    public IReadOnlyList<MapRoomCameraDock> GetDocksForCamera(NitroxId cameraId)
    {
        List<MapRoomCameraDock> packets = [];
        foreach (MapRoomCameraDock packet in dockStates.Values)
        {
            if (packet.CameraId == cameraId)
            {
                packets.Add(packet);
            }
        }
        packets.Sort((left, right) => left.Revision.CompareTo(right.Revision));
        return packets;
    }

    public void RetainControl(MapRoomCameraControl packet)
    {
        if (!packet.IsServerResponse)
        {
            return;
        }

        if (!packet.Granted)
        {
            return;
        }
        if (packet.IsControlling)
        {
            pendingControls[packet.CameraId] = packet;
        }
        else
        {
            pendingControls.Remove(packet.CameraId);
        }
    }

    public void RemoveControl(NitroxId cameraId) => pendingControls.Remove(cameraId);

    public IReadOnlyList<MapRoomCameraControl> GetPendingControls() => [.. pendingControls.Values];

    private static MapRoomCameraDock MergeEqualRevision(MapRoomCameraDock existing, MapRoomCameraDock incoming)
    {
        bool useIncomingLight = incoming.LightRevision >= existing.LightRevision;
        bool useIncomingComponent = incoming.ComponentRevision >= existing.ComponentRevision;
        return new MapRoomCameraDock(
            existing.CameraId,
            existing.MapRoomId,
            existing.DockingIndex,
            existing.Revision,
            isServerResponse: true,
            granted: true,
            isDocked: existing.IsDocked,
            cameraNumber: incoming.CameraNumber > 0 ? incoming.CameraNumber : existing.CameraNumber,
            lightOn: useIncomingLight ? incoming.LightOn : existing.LightOn,
            lightRevision: useIncomingLight ? incoming.LightRevision : existing.LightRevision,
            energy: useIncomingComponent ? incoming.Energy : existing.Energy,
            health: useIncomingComponent ? incoming.Health : existing.Health,
            componentRevision: useIncomingComponent ? incoming.ComponentRevision : existing.ComponentRevision,
            cameraTransform: incoming.CameraTransform ?? existing.CameraTransform);
    }

    private static bool HasSameCanonicalState(MapRoomCameraDock left, MapRoomCameraDock right) =>
        left.CameraId == right.CameraId &&
        left.MapRoomId == right.MapRoomId &&
        left.DockingIndex == right.DockingIndex &&
        left.Revision == right.Revision &&
        left.IsDocked == right.IsDocked &&
        left.CameraNumber == right.CameraNumber &&
        left.LightOn == right.LightOn &&
        left.LightRevision == right.LightRevision &&
        left.Energy.Equals(right.Energy) &&
        left.Health.Equals(right.Health) &&
        left.ComponentRevision == right.ComponentRevision &&
        Equals(left.CameraTransform, right.CameraTransform);

    private readonly record struct DockStateKey(NitroxId MapRoomId, int DockingIndex, NitroxId CameraId,
        long Revision, bool IsDocked)
    {
        public static DockStateKey From(MapRoomCameraDock packet) =>
            new(packet.MapRoomId, packet.DockingIndex, packet.CameraId, packet.Revision, packet.IsDocked);
    }
}
